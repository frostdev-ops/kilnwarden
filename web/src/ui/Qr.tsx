// A QR code as an SVG element (qrcode-generator, bundled): the otpauth:// URI for authenticator apps. Drawn with one
// path, light on dark inverted for scanning (dark modules on a white quiet zone).
import qrcode from "qrcode-generator";
import { useMemo } from "react";

export function qrPath(text: string): { size: number; d: string } {
  const q = qrcode(0, "M");
  q.addData(text, "Byte");
  q.make();
  const n = q.getModuleCount();
  let d = "";
  for (let r = 0; r < n; r++) for (let c = 0; c < n; c++) if (q.isDark(r, c)) d += `M${c + 4} ${r + 4}h1v1h-1z`;
  return { size: n + 8, d };
}

export function Qr({ text, label }: { text: string; label: string }) {
  const { size, d } = useMemo(() => qrPath(text), [text]);
  return (
    <svg className="wa-qr" viewBox={`0 0 ${size} ${size}`} role="img" aria-label={label} shapeRendering="crispEdges">
      <rect width={size} height={size} fill="#ffffff" />
      <path d={d} fill="#000000" />
    </svg>
  );
}
