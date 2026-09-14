import { MASCOTS, type MascotName } from '@/lib/mascots'

/**
 * มาสคอตคาปิบาร่า — ภาพจาก Freepik ที่ตัดพื้นหลังแล้ว เก็บไว้ที่ public/capybara/
 * ใส่ width/height จริงทุกครั้งเพื่อกันภาพกระตุกตอนโหลด (CLS)
 * เป็นภาพประดับล้วน จึง alt="" ข้อความรอบ ๆ สื่อความหมายอยู่แล้ว
 */
export function Mascot({
  name,
  className,
  priority = false,
}: {
  name: MascotName
  className?: string
  /** ใช้กับภาพที่อยู่บนสุดของหน้า จะโหลดทันทีแทนที่จะ lazy */
  priority?: boolean
}) {
  const [width, height] = MASCOTS[name]

  return (
    <img
      src={`/capybara/${name}.webp`}
      width={width}
      height={height}
      alt=""
      loading={priority ? 'eager' : 'lazy'}
      fetchPriority={priority ? 'high' : 'auto'}
      draggable={false}
      className={className}
    />
  )
}
