import { NavLink } from "react-router-dom";
import { useAuth } from "react-oidc-context";
import { ROUTES } from "../config/routes/paths";

const SignalsIcon = () => (
  <svg
    className="nav-icon"
    viewBox="0 0 16 16"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.5"
  >
    <path
      d="M1 11 L4 7 L7 9 L10 4 L13 6 L15 3"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
    <circle cx="1" cy="11" r="1" fill="currentColor" stroke="none" />
    <circle cx="15" cy="3" r="1" fill="currentColor" stroke="none" />
  </svg>
);

const BackofficeIcon = () => (
  <svg
    className="nav-icon"
    viewBox="0 0 16 16"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.5"
  >
    <path d="M3 4 H13 M3 8 H13 M3 12 H9" strokeLinecap="round" />
    <path
      d="M11.5 12 L13 13.5 L15.5 11"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </svg>
);

export default function Sidebar() {
  const auth = useAuth();
  const name = (auth.user?.profile?.name as string | undefined) ?? auth.user?.profile?.sub;

  return (
    <aside className="sidebar">
      <div className="sidebar-logo">
        <div className="sidebar-logo-mark">Y</div>
        <span className="sidebar-logo-name">Paygate</span>
      </div>
      <nav className="sidebar-nav">
        <NavLink
          to={ROUTES.signals.full()}
          className={({ isActive }) => `nav-item${isActive ? " active" : ""}`}
        >
          <SignalsIcon />
          Signals
        </NavLink>
        <NavLink
          to={ROUTES.backoffice.full()}
          className={({ isActive }) => `nav-item${isActive ? " active" : ""}`}
        >
          <BackofficeIcon />
          Backoffice
        </NavLink>
      </nav>
      {auth.isAuthenticated && (
        <div style={{ marginTop: "auto", padding: "12px", borderTop: "1px solid rgba(255,255,255,0.1)" }}>
          <div style={{ fontSize: "0.8rem", marginBottom: 8, opacity: 0.8 }}>{name}</div>
          <button className="nav-item" style={{ width: "100%", cursor: "pointer" }}
                  onClick={() => void auth.signoutRedirect()}>
            Logout
          </button>
        </div>
      )}
    </aside>
  );
}
