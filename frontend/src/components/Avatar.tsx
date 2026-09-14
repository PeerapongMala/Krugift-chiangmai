import { useState } from 'react'
import { Mascot } from '@/components/Mascot'
import { AVATAR_NAMES, type MascotName } from '@/lib/mascots'
import { cn } from '@/lib/utils'

const KEY = 'krugift.avatar'

const pickRandom = () => AVATAR_NAMES[Math.floor(Math.random() * AVATAR_NAMES.length)]

/** อ่านรูปที่เคยเลือกไว้ · ยังไม่เคยเลือก (หรืออ่าน localStorage ไม่ได้) ก็สุ่มมาให้ */
function loadSaved(): MascotName {
  try {
    const saved = localStorage.getItem(KEY)
    if (saved && (AVATAR_NAMES as string[]).includes(saved)) return saved as MascotName
  } catch {
    // โหมดส่วนตัวหรือเบราว์เซอร์ปิด site data ไว้ — สุ่มใหม่ทุกครั้งก็ยังใช้งานได้ปกติ
  }
  return pickRandom()
}

/**
 * รูปโปรไฟล์คาปิบาร่า — สุ่มมาให้ตอนเข้าครั้งแรก กดเพื่อเปลี่ยนเป็นตัวถัดไป
 * จำไว้ใน localStorage ของเครื่องผู้ใช้เอง ไม่ผูกกับข้อมูลในระบบ ซ้ำกับคนอื่นได้ไม่เป็นไร
 */
export function Avatar({ className }: { className?: string }) {
  const [name, setName] = useState<MascotName>(loadSaved)

  function showNext() {
    const picked = AVATAR_NAMES[(AVATAR_NAMES.indexOf(name) + 1) % AVATAR_NAMES.length]
    setName(picked)
    try {
      localStorage.setItem(KEY, picked)
    } catch {
      // เก็บไม่ได้ก็ยังเปลี่ยนรูปในหน้านี้ได้ แค่ไม่ถูกจำไว้รอบหน้า
    }
  }

  return (
    <button
      type="button"
      onClick={showNext}
      title="กดเพื่อเปลี่ยนรูป"
      aria-label="เปลี่ยนรูปโปรไฟล์"
      className={cn(
        'flex size-9 shrink-0 items-center justify-center overflow-hidden rounded-full bg-accent',
        'transition hover:brightness-95 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
        className,
      )}
    >
      <Mascot name={name} className="h-8 w-auto" />
    </button>
  )
}
