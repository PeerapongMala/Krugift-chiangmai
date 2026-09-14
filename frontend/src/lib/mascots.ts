/**
 * มาสคอตคาปิบาร่าทั้งหมดที่มีใน public/capybara/
 * ตัวเลขคือขนาดจริงของไฟล์ ใส่ลง <img width/height> เพื่อกันภาพกระตุกตอนโหลด (CLS)
 * แยกจาก Mascot.tsx เพราะไฟล์ .tsx ที่ export ค่าที่ไม่ใช่ component จะทำให้ fast refresh ไม่ทำงาน
 */
export const MASCOTS = {
  orange: [344, 512],
  crown: [435, 512],
  watermelon: [451, 512],
  frog: [512, 508],
  study: [512, 508],
  bath: [451, 512],
  eating: [371, 512],
  onsen: [512, 437],
  sleeping: [512, 317],
} as const

export type MascotName = keyof typeof MASCOTS

/**
 * ตัวเลือกรูปโปรไฟล์ — 8 ตัว ไม่รวม orange
 * เพราะ orange ถูกใช้เป็นโลโก้บน header และหน้า login อยู่แล้ว จะได้ไม่ซ้ำกับโลโก้
 */
export const AVATAR_NAMES: MascotName[] = [
  'crown',
  'watermelon',
  'frog',
  'study',
  'bath',
  'eating',
  'onsen',
  'sleeping',
]
