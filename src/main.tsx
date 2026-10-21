import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
import "./index.css";

// Tarayıcının varsayılan sağ tık menüsünü kapat (inputlar hariç).
document.addEventListener("contextmenu", (e) => {
  if (!(e.target instanceof HTMLInputElement)) e.preventDefault();
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
