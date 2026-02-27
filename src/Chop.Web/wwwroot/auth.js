// Minimal helper for login autofill/binding edge-cases.
// Reads raw input values from DOM so server-side submit can use them.
window.chopAuth = window.chopAuth || {};
window.chopAuth.getValue = (id) => {
  const el = document.getElementById(id);
  return el ? el.value : null;
};

window.chopAuth.persistSession = (accessToken, refreshToken) => {
  const maxAge = 60 * 60 * 24 * 30;
  document.cookie = `chop_access_token=${encodeURIComponent(accessToken || "")}; Path=/; Max-Age=${maxAge}; SameSite=Lax`;
  document.cookie = `chop_refresh_token=${encodeURIComponent(refreshToken || "")}; Path=/; Max-Age=${maxAge}; SameSite=Lax`;
};

window.chopAuth.clearSession = () => {
  document.cookie = "chop_access_token=; Path=/; Max-Age=0; SameSite=Lax";
  document.cookie = "chop_refresh_token=; Path=/; Max-Age=0; SameSite=Lax";
};
