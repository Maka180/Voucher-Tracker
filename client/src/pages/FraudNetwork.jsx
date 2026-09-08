import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import ForceGraph2D from 'react-force-graph-2d';
import { api, getSession, clearSession } from '../api/client';

const NODE_COLORS = {
  voucher: '#3F4A2B',
  phone: '#7C8A54',
  ip: '#A6432F',
};

export default function FraudNetwork() {
  const [graphData, setGraphData] = useState({ nodes: [], links: [] });
  const [selected, setSelected] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const containerRef = useRef(null);
  const { fullName } = getSession();
  const navigate = useNavigate();

  useEffect(() => {
    async function load() {
      try {
        const data = await api.getFraudNetwork();
        setGraphData({
          nodes: data.nodes,
          links: data.edges.map((e) => ({ source: e.source, target: e.target, relation: e.relation })),
        });
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

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

      <main className="main-content network-content">
        <h1>Fraud network</h1>
        <p className="ink-muted">
          Each line connects a flagged voucher to its recipient phone or an IP address that
          attempted redemption. A phone or IP touching many vouchers is a likely fraud ring.
        </p>

        <div className="network-legend">
          <span><span className="dot" style={{ background: NODE_COLORS.voucher }} /> Voucher</span>
          <span><span className="dot" style={{ background: NODE_COLORS.phone }} /> Phone</span>
          <span><span className="dot" style={{ background: NODE_COLORS.ip }} /> IP address</span>
        </div>

        {error && <p className="error-text">{error}</p>}

        <div className="network-graph-wrapper" ref={containerRef}>
          {loading ? (
            <p className="ink-muted">Loading…</p>
          ) : graphData.nodes.length === 0 ? (
            <p className="ink-muted">No flagged vouchers to visualize yet.</p>
          ) : (
            <ForceGraph2D
              graphData={graphData}
              nodeLabel={(node) => node.label}
              nodeColor={(node) => NODE_COLORS[node.type] || '#999'}
              nodeRelSize={5}
              linkColor={() => '#DCD8C9'}
              onNodeClick={(node) => setSelected(node)}
              width={containerRef.current?.clientWidth || 600}
              height={480}
              backgroundColor="#FFFFFF"
            />
          )}
        </div>

        {selected && (
          <div className="network-detail-panel">
            <div className="flag-type-row">
              <span
                className="flag-type-pill"
                style={{ color: NODE_COLORS[selected.type], borderColor: NODE_COLORS[selected.type] }}
              >
                {selected.type}
              </span>
            </div>
            <h3 style={{ marginTop: '0.5rem' }}>{selected.label}</h3>
            <p className="flag-explanation">
              {selected.detail || 'No additional detail for this node.'}
            </p>
          </div>
        )}
      </main>
    </div>
  );
}