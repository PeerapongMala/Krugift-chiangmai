import { Pencil } from 'lucide-react'
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
    // group ทำให้ไอคอนดินสอโผล่ตอนเอาเมาส์ชี้ · บนมือถือไม่มี hover จึงยังต้องพึ่ง aria-label กับขนาดปุ่มที่กดง่าย
    <button
      type="button"
      onClick={showNext}
      title="กดเพื่อเปลี่ยนรูป"
      aria-label="เปลี่ยนรูปโปรไฟล์"
      className={cn(
        'group relative flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-full',
        'bg-accent ring-1 ring-transparent transition',
        'hover:ring-primary/40 hover:brightness-95 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
        className,
      )}
    >
      <Mascot name={name} className="h-9 w-auto transition group-hover:scale-105" />

      {/* ป้ายดินสอคลุมทับตอนชี้ บอกว่ารูปนี้เปลี่ยนได้ */}
      <span
        aria-hidden="true"
        className="absolute inset-0 grid place-items-center bg-foreground/45 text-background opacity-0 transition
                   group-hover:opacity-100 group-focus-visible:opacity-100"
      >
        <Pencil className="size-4" />
      </span>
    </button>
  )
}
