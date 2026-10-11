// The site never shows em dashes. Square content (names, descriptions) is cleaned on the way in,
// and the build sweeps every generated page as a safety net, so new products stay clean too.
const EM = /\s*(?:—|&mdash;|&#8212;|&#x2014;)\s*/gi;

/** Prose: "Black and white — the oldest argument" -> "Black and white, the oldest argument" */
export const noEmDash = (s) => String(s ?? '').replace(EM, ', ');

/** Names and labels: "Noir et Blanc — The Ribbon Collection" -> "Noir et Blanc - The Ribbon Collection" */
export const noEmDashName = (s) => String(s ?? '').replace(EM, ' - ');
