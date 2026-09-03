const BASE_URL = 'http://localhost:5137/api';

function getToken() {
  return localStorage.getItem('token');
}

async function request(path, options = {}) {
  const token = getToken();
  const headers = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...options.headers,
  };

  const response = await fetch(`${BASE_URL}${path}`, { ...options, headers });
  const contentType = response.headers.get('content-type') || '';
  const body = contentType.includes('application/json')
    ? await response.json()
    : await response.text();

  if (!response.ok) {
    const message = typeof body === 'string' ? body : body?.message || 'Request failed';
    throw new Error(message);
  }

  return body;
}

export const api = {
  register: (fullName, phone, password) =>
    request('/Auth/register', { method: 'POST', body: JSON.stringify({ fullName, phone, password }) }),

  login: (phone, password) =>
    request('/Auth/login', { method: 'POST', body: JSON.stringify({ phone, password }) }),

  createVoucher: (amount, recipientPhone) =>
    request('/Vouchers', { method: 'POST', body: JSON.stringify({ amount, recipientPhone }) }),

  getMyVouchers: () => request('/Vouchers/mine'),

  redeemVoucher: (id, pin) =>
    request(`/Vouchers/${id}/redeem`, { method: 'POST', body: JSON.stringify({ pin }) }),
};

export function saveSession(token, fullName, role) {
  localStorage.setItem('token', token);
  localStorage.setItem('fullName', fullName);
  localStorage.setItem('role', role);
}

export function clearSession() {
  localStorage.removeItem('token');
  localStorage.removeItem('fullName');
  localStorage.removeItem('role');
}

export function getSession() {
  return {
    token: getToken(),
    fullName: localStorage.getItem('fullName'),
    role: localStorage.getItem('role'),
  };
}