/**
 * มาสคอตคาปิบาร่า — วาดเป็น inline SVG ไม่ใช่ไฟล์รูป
 * เหตุผล: คมทุกขนาด, ไม่มี network request, และเปลี่ยนสีตามธีมได้เพราะใช้ CSS variable (--capy*)
 * เป็นภาพประดับล้วน จึง aria-hidden — ข้อความรอบ ๆ สื่อความหมายอยู่แล้ว
 */
export function Capybara({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 64 56" className={className} aria-hidden="true" focusable="false">
      {/* หู */}
      <ellipse cx="16" cy="13" rx="6" ry="5.2" fill="var(--capy-dark)" />
      <ellipse cx="48" cy="13" rx="6" ry="5.2" fill="var(--capy-dark)" />
      <ellipse cx="16" cy="13.6" rx="2.8" ry="2.3" fill="var(--capy-ear)" />
      <ellipse cx="48" cy="13.6" rx="2.8" ry="2.3" fill="var(--capy-ear)" />

      {/* หัวทรงป้าน ๆ ตามแบบคาปิบาร่า */}
      <path d="M32 6c14 0 23 8 23 21 0 14-10 23-23 23S9 41 9 27C9 14 18 6 32 6Z" fill="var(--capy)" />

      {/* ปาก */}
      <ellipse cx="32" cy="39" rx="14" ry="10" fill="var(--capy-muzzle)" />

      {/* หนวด */}
      <g stroke="var(--capy-dark)" strokeWidth="1" strokeLinecap="round" opacity="0.45" fill="none">
        <path d="M19 35.5c-4-1-8-2-13-4.5" />
        <path d="M19 39c-4 0-8 .5-13 1.5" />
        <path d="M45 35.5c4-1 8-2 13-4.5" />
        <path d="M45 39c4 0 8 .5 13 1.5" />
      </g>

      {/* ตา */}
      <ellipse cx="21" cy="24" rx="2.8" ry="3.2" fill="var(--capy-eye)" />
      <ellipse cx="43" cy="24" rx="2.8" ry="3.2" fill="var(--capy-eye)" />
      <circle cx="22" cy="22.8" r="1" fill="oklch(1 0 0 / 0.85)" />
      <circle cx="44" cy="22.8" r="1" fill="oklch(1 0 0 / 0.85)" />

      {/* จมูก + ปาก */}
      <ellipse cx="32" cy="33" rx="6" ry="4" fill="var(--capy-dark)" />
      <g stroke="var(--capy-dark)" strokeWidth="1.6" strokeLinecap="round" fill="none">
        <path d="M32 37v2.5" />
        <path d="M27.5 41.5q4.5 3.5 9 0" />
      </g>
    </svg>
  )
}
