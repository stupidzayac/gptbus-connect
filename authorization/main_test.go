package main

import (
	"crypto/sha256"
	"encoding/base64"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

func TestConsentAndSingleUseExchange(t *testing.T) {
	var issued atomic.Int32
	var name string
	api := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Header.Get("Authorization") != "Bearer test-session" {
			t.Error("session mismatch")
			w.WriteHeader(401)
			return
		}
		var data any
		switch r.URL.Path {
		case "/api/user/self":
			data = map[string]int{"id": 7}
		case "/api/token/":
			if r.Method == "POST" {
				var in struct {
					Name        string
					RemainQuota int  `json:"remain_quota"`
					Unlimited   bool `json:"unlimited_quota"`
				}
				_ = json.NewDecoder(r.Body).Decode(&in)
				if in.RemainQuota != 250000 || in.Unlimited {
					t.Error("wrong spending limit")
				}
				name = in.Name
				issued.Add(1)
			} else {
				data = map[string]any{"items": []any{map[string]any{"name": name, "id": 11}}}
			}
		case "/api/token/11/key":
			data = map[string]string{"key": "test-device"}
		default:
			t.Errorf("unexpected request %s", r.URL.Path)
		}
		json.NewEncoder(w).Encode(map[string]any{"success": true, "data": data})
	}))
	defer api.Close()
	b := &broker{api: api.URL, http: api.Client(), grants: map[string]grant{}}
	post := func(path, origin string, payload any) *httptest.ResponseRecorder {
		body, _ := json.Marshal(payload)
		r := httptest.NewRequest("POST", path, strings.NewReader(string(body)))
		r.Header.Set("Authorization", "Bearer test-session")
		r.Header.Set("Origin", origin)
		r.Header.Set("Content-Type", "application/json")
		w := httptest.NewRecorder()
		b.ServeHTTP(w, r)
		return w
	}
	verifier := base64.RawURLEncoding.EncodeToString(randomBytes(32))
	hash := sha256.Sum256([]byte(verifier))
	consent := map[string]any{"challenge": base64.RawURLEncoding.EncodeToString(hash[:]), "userID": 7, "limit": "trial"}
	if post("/connect/approve", "https://example.org", consent).Code != 403 {
		t.Fatal("invalid origin accepted")
	}
	approved := post("/connect/approve", site, consent)
	var result struct{ Code string }
	if json.Unmarshal(approved.Body.Bytes(), &result) != nil || !value43.MatchString(result.Code) || issued.Load() != 0 {
		t.Fatal("incorrect approval")
	}
	exchange := func(v string) *httptest.ResponseRecorder {
		return post("/connect/exchange", "", map[string]string{"code": result.Code, "verifier": v})
	}
	if exchange(strings.Repeat("A", 43)).Code != 403 {
		t.Fatal("invalid verifier accepted")
	}
	res := exchange(verifier)
	if res.Code != 200 || !strings.Contains(res.Body.String(), "sk-test-device") || issued.Load() != 1 {
		t.Fatal("key missing")
	}
	if exchange(verifier).Code != 403 || issued.Load() != 1 {
		t.Fatal("code redeemed twice")
	}
	b.grants[result.Code] = grant{challenge: consent["challenge"].(string), expires: time.Now().Add(-time.Second)}
	if exchange(verifier).Code != 403 {
		t.Fatal("expired code accepted")
	}
}
