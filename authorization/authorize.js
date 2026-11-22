(() => {
  const el = id => document.getElementById(id);
  const params = new URLSearchParams(location.hash.slice(1));
  const port = params.get('port'), state = params.get('state'), challenge = params.get('challenge');
  const valid = x => /^[A-Za-z0-9_-]{43}$/.test(x || '');
  if (!valid(state) || !valid(challenge) || !/^[1-9]\d{3,4}$/.test(port || '') || +port < 1024 || +port > 65535) {
    el('status').textContent = '请从桌面工具点击「登录 GPTBus」重新开始。';
    return;
  }
  el('pair').textContent = state.slice(0,8);
  el('cancel').hidden = false;
  let account = null, loginTab = null, checking = null, busy = false, done = false;
  const askSession = async () => {
    const refresh = async () => {
      const r = await fetch('/api/user/auth/refresh', {method:'POST',credentials:'same-origin', cache:'no-store'});
      if (r.status === 401 || r.status === 403) return null;
      const j = await r.json();
      if (!r.ok) throw new Error();
      return j.success && j.data?.access_token && j.data?.user?.id ? j.data : null;
    };
    return navigator.locks ? navigator.locks.request('new-api:auth-refresh', refresh) : refresh();
  };
  const show = user => {
    account = user?.id ?? null;
    el('login').hidden = !!user;
    el('approve').hidden = el('consent').hidden = !user;
    el('status').textContent = user ? '当前账号：' + user.username : '请先登录你的 GPTBus 账号。';
  };
  async function check() {
    if (done || busy || checking) return;
    checking = askSession();
    try {
      const session = await checking;
      if (busy || done) return;
      show(session?.user);
      if (session && loginTab) {
        try { loginTab.close(); window.focus(); } catch {}
        loginTab = null;
        el('hint').textContent = '';
      }
    } catch { if (!done && !busy) { show(null); el('status').textContent = '暂时无法确认登录状态，请稍后重试。'; } }
    finally { checking = null; }
  }
  function finish(code, error) {
    done = true;
    const to = new URL('http://127.0.0.1:' + port + '/callback');
    to.searchParams.set('state',state);
    if (code) to.searchParams.set('code',code);
    else to.searchParams.set('error',error);
    location.replace(to.href);
  }
  el('login').onclick = () => {
    loginTab = window.open('about:blank','_blank','popup,width=620,height=800');
    if (!loginTab) { el('hint').textContent = '请允许浏览器为 gptbus.ai 打开弹出窗口，再点一次「登录 GPTBus」。'; return; }
    loginTab.opener = null;
    loginTab.location.replace('/sign-in');
    el('hint').textContent = '完成登录后，请在此页确认连接。';
  };
  if ('BroadcastChannel' in window) {
    const sync = new BroadcastChannel('new-api:auth-session');
    sync.onmessage = () => void check();
  }
  window.addEventListener('storage', e => { if (e.key === 'new-api:auth-session:event') void check(); });
  window.addEventListener('focus',check);
  el('cancel').onclick = () => finish(null,'access_denied');
  el('approve').onclick = async () => {
    if (busy || !account) return;
    busy = true; el('approve').disabled = el('cancel').disabled = true;
    try {
      const session = await askSession();
      if (!session || session.user.id !== account) {
        show(session?.user); el('hint').textContent = '登录状态已变化，请重新确认当前账号。'; return;
      }
      const r = await fetch('/connect/approve', {method:'POST',cache:'no-store',credentials:'omit',
        headers:{'Content-Type':'application/json',Authorization:'Bearer ' + session.access_token},
        body:JSON.stringify({challenge,userID:account,limit:el('limit').value})});
      if (!r.ok) throw new Error();
      const {code} = await r.json();
      if (!valid(code)) throw new Error();
      finish(code);
    } catch { el('status').textContent = '暂时无法完成授权，请重试。'; }
    finally { busy = false; el('approve').disabled = el('cancel').disabled = false; }
  };
  void check();
})();
