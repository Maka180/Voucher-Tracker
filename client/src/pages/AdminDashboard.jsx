import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, getSession, clearSession } from '../api/client';

export default function AdminDashboard() {
  const [vouchers, setVouchers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [resolvingId, setResolvingId] = useState(null);

  const { fullName } = getSession();
  const navigate = useNavigate();

  async function loadFlagged() {
    setLoading(true);
    try {
      const data = await api.getFlaggedVouchers();
      setVouchers(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadFlagged();
  }, []);

  async function handleResolve(flagId) {
    setResolvingId(flagId);
    try {
      await api.resolveFlag(flagId);
      await loadFlagged();
    } catch (err) {
      setError(err.message);
    } finally {
      setResolvingId(null);
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
        <p className="sidebar-user">{fullName} · Admin</p>
        <button className="sidebar-logout" onClick={handleLogout}>Sign out</button>
      </aside>

      <main className="main-content admin-content">
        <h1>Flagged vouchers</h1>
        <p className="ink-muted">Vouchers held for review, with AI-assisted fraud explanations.</p>

        {error && <p className="error-text">{error}</p>}

        {loading ? (
          <p className="ink-muted">Loading…</p>
        ) : vouchers.length === 0 ? (
          <p className="ink-muted">No flagged vouchers right now.</p>
        ) : (
          <div className="flag-list">
            {vouchers.map((v) => (
              <div className="flag-card" key={v.id}>
                <div className="flag-card-header">
                  <span className="voucher-amount">R{v.amount}</span>
                  <span className="voucher-recipient">→ {v.recipientPhone}</span>
                  <span className="voucher-date">{new Date(v.createdAt).toLocaleString()}</span>
                </div>

                {v.flags.map((flag) => (
                  <div className="flag-detail" key={flag.id}>
                    <div className="flag-type-row">
                      <span className="flag-type-pill">{flag.flagType}</span>
                      {flag.resolved && <span className="flag-resolved-pill">Resolved</span>}
                    </div>
                    <p className="flag-explanation">
                      {flag.aiExplanation || 'No AI explanation available for this flag.'}
                    </p>
                    {!flag.resolved && (
                      <button
                        className="btn-primary btn-small"
                        onClick={() => handleResolve(flag.id)}
                        disabled={resolvingId === flag.id}
                      >
                        {resolvingId === flag.id ? 'Resolving…' : 'Mark resolved'}
                      </button>
                    )}
                  </div>
                ))}
              </div>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}