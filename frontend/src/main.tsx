import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Link, NavLink, Route, Routes, useParams } from "react-router";
import { EnvironmentPage } from "./environment";
import { ExhibitDetail, RegistryPage } from "./exhibits";
import "./styles.css";

function NotFound() {
  return (
    <>
      <h1>Puslapis nerastas</h1>
      <p><Link to="/">Grįžti į aplinkos stebėjimo puslapį</Link></p>
    </>
  );
}

function ExhibitRoute() {
  const { id } = useParams();
  return id && /^\d+$/.test(id) ? <ExhibitDetail key={id} id={id} /> : <NotFound />;
}

function App() {
  return (
    <>
      <a className="skip-link" href="#main-content">Pereiti prie turinio</a>
      <header className="site-header">
        <div className="header-content">
          <Link className="brand" to="/">Muziejaus saugykla</Link>
          <nav aria-label="Pagrindinis meniu">
            <NavLink to="/" end>Aplinkos stebėjimas</NavLink>
            <NavLink to="/exhibits">Eksponatų registras</NavLink>
          </nav>
        </div>
      </header>
      <main id="main-content" tabIndex={-1} className="workspace">
        <Routes>
          <Route path="/" element={<EnvironmentPage />} />
          <Route path="/exhibits" element={<RegistryPage />} />
          <Route path="/exhibits/:id" element={<ExhibitRoute />} />
          <Route path="*" element={<NotFound />} />
        </Routes>
      </main>
    </>
  );
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
);
