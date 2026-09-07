import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, getSession, clearSession } from '../api/client';

export default function Dashboard() {
  const [vouchers, setVouchers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [amount, setAmount] = useState('');
  const [recipientPhone, setRecipientPhone] = useState('');
  const [creating, setCreating] = useState(false);
  const [newPin, setNewPin] = useState(null);

  const { fullName } = getSession();
  const navigate = useNavigate();

  async function loadVouchers() {
    try {
      const data = await api.getMyVouchers();
      setVouchers(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadVouchers();
  }, []);

  async function handleCreate(e) {
    e.preventDefault();
    setError('');
    setCreating(true);
    setNewPin(null);
    try {
      const voucher = await api.createVoucher(Number(amount), recipientPhone);
      setNewPin(voucher.pin);
      setAmount('');
      setRecipientPhone('');
      await loadVouchers();
    } catch (err) {
      setError(err.message);
    } finally {
      setCreating(false);
    }
  }

  function handleLogout() {
    clearSession();
    navigate('/login');
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <h2 className="sidebar-title">Voucher Tracker</h2>
        <p className="sidebar-user">{fullName}</p>
        <button className="sidebar-logout" onClick={handleLogout}>Sign out</button>
      </aside>

      <main className="main-content">
        <h1>Create a voucher</h1>

        <form onSubmit={handleCreate} className="voucher-form">
          <div className="field">
            <label htmlFor="amount">Amount (R)</label>
            <input
              id="amount"
              type="number"
              min="1"
              step="0.01"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              required
            />
          </div>

          <div className="field">
            <label htmlFor="recipientPhone">Recipient phone</label>
            <input
              id="recipientPhone"
              type="tel"
              value={recipientPhone}
              onChange={(e) => setRecipientPhone(e.target.value)}
              required
            />
          </div>

          {error && <p className="error-text">{error}</p>}

          <button className="btn-primary" type="submit" disabled={creating}>
            {creating ? 'Creating…' : 'Create voucher'}
          </button>
        </form>

        {newPin && (
          <div className="pin-reveal">
            <p>Voucher created. Share this PIN with the recipient — it won't be shown again:</p>
            <div className="pin-code">{newPin}</div>
          </div>
        )}

        <h1 className="vouchers-heading">Your vouchers</h1>

        {loading ? (
          <p className="ink-muted">Loading…</p>
        ) : vouchers.length === 0 ? (
          <p className="ink-muted">No vouchers yet.</p>
        ) : (
          <div className="voucher-list">
            {vouchers.map((v) => (
              <div className="voucher-row" key={v.id}>
                <span className="voucher-amount">R{v.amount}</span>
                <span className="voucher-recipient">→ {v.recipientPhone}</span>
                <span className={`status-dot status-${v.status.toLowerCase()}`}>{v.status}</span>
                <span className="voucher-date">{new Date(v.createdAt).toLocaleDateString()}</span>
              </div>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}