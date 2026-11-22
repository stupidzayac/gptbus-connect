package main

import (
	"bytes"
	"crypto/rand"
	"crypto/sha256"
	"crypto/subtle"
	_ "embed"
	"encoding/base64"
	"encoding/hex"
	"encoding/json"
	"errors"
	"io"
	"net"
	"net/http"
	"net/url"
	"regexp"
	"strconv"
	"strings"
	"sync"
	"time"
)

//go:embed authorize.html
var page []byte

//go:embed authorize.js
var script []byte

const site = "https://gptbus.ai"

var value43 = regexp.MustCompile("^[A-Za-z0-9_-]{43}$")
var rejected = errors.New("request unavailable")

type grant struct {
	session, challenge, name, browserIP string
	quota                               int
	expires                             time.Time
}

type broker struct {
	api    string
	http   *http.Client
	mu     sync.Mutex
	grants map[string]grant
}

func randomBytes(n int) []byte {
	b := make([]byte, n)
	if _, err := rand.Read(b); err != nil {
		panic(err)
	}
	return b
}

func (b *broker) expire() {
	b.mu.Lock()
	defer b.mu.Unlock()
	for code, g := range b.grants {
		if !time.Now().Before(g.expires) {
			delete(b.grants, code)
		}
	}
}

func (b *broker) upstream(method, path string, g grant, input, output any) error {
	var body bytes.Buffer
	if input != nil {
		if err := json.NewEncoder(&body).Encode(input); err != nil {
			return err
		}
	}
	req, err := http.NewRequest(method, b.api+path, &body)
	if err != nil {
		return rejected
	}
	req.Header.Set("Authorization", g.session)
	if ip := net.ParseIP(g.browserIP); ip != nil {
		req.Header.Set("X-Forwarded-For", ip.String())
		req.Header.Set("X-Real-IP", ip.String())
	}
	req.Header.Set("Content-Type", "application/json")
	resp, err := b.http.Do(req)
	if err != nil {
		return rejected
	}
	defer resp.Body.Close()
	var result struct {
		Success bool
		Data    json.RawMessage
	}
	if resp.StatusCode != 200 || json.NewDecoder(io.LimitReader(resp.Body, 1<<20)).Decode(&result) != nil || !result.Success {
		return rejected
	}
	if output != nil {
		return json.Unmarshal(result.Data, output)
	}
	return nil
}

func reply(w http.ResponseWriter, code int, payload any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(code)
	_ = json.NewEncoder(w).Encode(payload)
}

func decode(w http.ResponseWriter, r *http.Request, out any) bool {
	if r.Method != "POST" {
		w.WriteHeader(405)
		return false
	}
	if strings.Split(r.Header.Get("Content-Type"), ";")[0] != "application/json" {
		w.WriteHeader(415)
		return false
	}
	d := json.NewDecoder(http.MaxBytesReader(w, r.Body, 4096))
	d.DisallowUnknownFields()
	if d.Decode(out) != nil || d.Decode(new(any)) != io.EOF {
		w.WriteHeader(400)
		return false
	}
	return true
}

func (b *broker) approve(w http.ResponseWriter, r *http.Request) {
	if r.Header.Get("Origin") != site {
		w.WriteHeader(403)
		return
	}
	var input struct {
		Challenge string
		UserID    int
		Limit     string
	}
	if !decode(w, r, &input) {
		return
	}
	auth := r.Header.Get("Authorization")
	if !value43.MatchString(input.Challenge) || input.UserID <= 0 || (input.Limit != "account" && input.Limit != "trial") {
		w.WriteHeader(400)
		return
	}
	if !strings.HasPrefix(auth, "Bearer ") || len(auth) > 8192 {
		w.WriteHeader(401)
		return
	}
	var user struct{ ID int }
	g := grant{session: auth, challenge: input.Challenge, browserIP: r.Header.Get("X-Real-IP")}
	if b.upstream("GET", "/api/user/self", g, nil, &user) != nil || user.ID != input.UserID {
		w.WriteHeader(401)
		return
	}
	quota := 0
	if input.Limit == "trial" {
		quota = 250000
	} // GPTBus uses 500,000 units per USD.
	code := base64.RawURLEncoding.EncodeToString(randomBytes(32))
	g.name = "GPTBus Connect " + hex.EncodeToString(randomBytes(8))
	g.quota, g.expires = quota, time.Now().Add(time.Minute)
	b.mu.Lock()
	if len(b.grants) >= 1024 {
		b.mu.Unlock()
		w.WriteHeader(429)
		return
	}
	b.grants[code] = g
	b.mu.Unlock()
	reply(w, 200, map[string]string{"code": code})
}

func (b *broker) tokenID(g grant) (int, error) {
	var list struct {
		Items []struct {
			ID   int
			Name string
		}
	}
	for _, path := range []string{"/api/token/?p=1&size=10", "/api/token/search?keyword=" + url.QueryEscape(g.name)} {
		if b.upstream("GET", path, g, nil, &list) == nil {
			for _, t := range list.Items {
				if t.Name == g.name && t.ID > 0 {
					return t.ID, nil
				}
			}
		}
	}
	return 0, rejected
}

func (b *broker) issue(g grant) (string, error) {
	input := map[string]any{"name": g.name, "expired_time": -1, "remain_quota": g.quota, "unlimited_quota": g.quota == 0, "allow_user_quota_query": false}
	createErr := b.upstream("POST", "/api/token/", g, input, nil)
	// The token list is owner-scoped. The random name also correlates a creation
	// whose response was lost without ever submitting the creation twice.
	id, err := b.tokenID(g)
	if err != nil {
		return "", rejected
	}
	path := "/api/token/" + strconv.Itoa(id)
	var t struct{ Key string }
	if createErr != nil || b.upstream("POST", path+"/key", g, nil, &t) != nil || t.Key == "" {
		_ = b.upstream("DELETE", path, g, nil, nil)
		return "", rejected
	}
	return "sk-" + strings.TrimPrefix(t.Key, "sk-"), nil
}

func (b *broker) exchange(w http.ResponseWriter, r *http.Request) {
	var in struct{ Code, Verifier string }
	if !decode(w, r, &in) {
		return
	}
	if !value43.MatchString(in.Code) || !value43.MatchString(in.Verifier) {
		w.WriteHeader(400)
		return
	}
	sum := sha256.Sum256([]byte(in.Verifier))
	challenge := base64.RawURLEncoding.EncodeToString(sum[:])
	b.mu.Lock()
	g, ok := b.grants[in.Code]
	ok = ok && time.Now().Before(g.expires) && subtle.ConstantTimeCompare([]byte(challenge), []byte(g.challenge)) == 1
	if ok {
		delete(b.grants, in.Code)
	}
	b.mu.Unlock()
	if !ok {
		w.WriteHeader(403)
		return
	}
	key, err := b.issue(g)
	if err != nil {
		w.WriteHeader(502)
		return
	}
	reply(w, 200, map[string]string{"key": key, "name": g.name})
}

func (b *broker) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	for k, v := range map[string]string{"Cache-Control": "no-store", "Referrer-Policy": "no-referrer", "X-Content-Type-Options": "nosniff", "X-Frame-Options": "DENY", "Content-Security-Policy": "default-src 'none'; script-src 'self'; style-src 'unsafe-inline'; connect-src 'self'; img-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'"} {
		w.Header().Set(k, v)
	}
	switch r.URL.Path {
	case "/connect/approve":
		b.approve(w, r)
	case "/connect/exchange":
		b.exchange(w, r)
	case "/connect/authorize", "/connect/authorize.js":
		if r.Method != "GET" && r.Method != "HEAD" {
			w.WriteHeader(405)
			return
		}
		if r.URL.Path == "/connect/authorize" {
			w.Header().Set("Content-Type", "text/html; charset=utf-8")
			w.Write(page)
		} else {
			w.Header().Set("Content-Type", "text/javascript; charset=utf-8")
			w.Write(script)
		}
	default:
		http.NotFound(w, r)
	}
}

func main() {
	b := &broker{api: "http://127.0.0.1:3000", http: &http.Client{Timeout: 8 * time.Second, CheckRedirect: func(*http.Request, []*http.Request) error { return http.ErrUseLastResponse }}, grants: map[string]grant{}}
	go func() {
		for range time.Tick(10 * time.Second) {
			b.expire()
		}
	}()
	s := &http.Server{Addr: "127.0.0.1:14137", Handler: b, ReadHeaderTimeout: 4 * time.Second, ReadTimeout: 8 * time.Second, WriteTimeout: 40 * time.Second, IdleTimeout: 30 * time.Second, MaxHeaderBytes: 16384}
	if err := s.ListenAndServe(); err != nil {
		panic(err)
	}
}
