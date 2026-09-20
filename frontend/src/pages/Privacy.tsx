import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { Credit } from '@/components/Credit'
import { routes } from '@/lib/keys'

/**
 * นโยบายความเป็นส่วนตัว · หน้าสาธารณะ ไม่ต้องล็อกอิน
 * Google บังคับให้มี URL นี้ก่อนเปิดให้คนทั่วไปล็อกอินด้วยบัญชี Google ได้
 * เนื้อหาต้องตรงกับสิ่งที่ระบบทำจริง ถ้าเก็บข้อมูลเพิ่มต้องกลับมาแก้หน้านี้ด้วย
 */
export default function Privacy() {
  return (
    <div className="auth-bg min-h-svh px-4 py-10">
      <main className="mx-auto max-w-2xl rounded-xl border bg-card p-6">
        <h1 className="font-heading text-xl font-semibold">นโยบายความเป็นส่วนตัว</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          เว็บนี้เป็นระบบดูคะแนนของครูคณิตหนึ่งท่าน ใช้กับนักเรียนในห้องที่ครูสอนเท่านั้น
        </p>

        <Section title="ข้อมูลที่เก็บ">
          <Item label="จากบัญชี Google">
            ชื่อและอีเมล ใช้ยืนยันว่าเป็นใครตอนเข้าสู่ระบบ ไม่ได้ขอสิทธิ์อ่านอีเมลหรือไฟล์ใด ๆ
          </Item>
          <Item label="จากไฟล์คะแนนของครู">
            คำนำหน้า ชื่อ นามสกุล ชื่อเล่น รหัสนักเรียน เลขที่ในห้อง และคะแนนของแต่ละรายการ
          </Item>
          <Item label="จากการใช้งาน">
            ข้อความที่นักเรียนสอบถามครูเรื่องคะแนน และประวัติว่าคะแนนถูกแก้เมื่อไหร่โดยครูคนไหน
          </Item>
        </Section>

        <Section title="ใครเห็นข้อมูลบ้าง">
          <Item label="ครู">เห็นเฉพาะห้องที่ตัวเองสร้าง ครูคนอื่นเห็นข้ามกันไม่ได้</Item>
          <Item label="นักเรียน">เห็นเฉพาะคะแนนของตัวเอง ไม่เห็นของเพื่อน และไม่เห็นรายการที่ครูตั้งให้ซ่อนไว้</Item>
          <Item label="หน้าดูคะแนนด่วน">
            ดูได้โดยไม่ต้องเข้าสู่ระบบ แต่ต้องกรอกห้อง เลขที่ และรหัสนักเรียนให้ถูกครบทั้งสามอย่าง
            และหน้านี้ไม่แสดงรายชื่อนักเรียน
          </Item>
        </Section>

        <Section title="ข้อมูลเก็บไว้ที่ไหน">
          <p>
            เก็บในฐานข้อมูล PostgreSQL ของ Neon (เซิร์ฟเวอร์ในสิงคโปร์) และเว็บรันอยู่บน Render
            การเชื่อมต่อทั้งหมดเข้ารหัสด้วย HTTPS
          </p>
        </Section>

        <Section title="สิ่งที่เราไม่ทำ">
          <p>ไม่ขายหรือส่งต่อข้อมูลให้ใคร ไม่มีโฆษณา ไม่มีเครื่องมือติดตามพฤติกรรมของบุคคลที่สาม</p>
        </Section>

        <Section title="ขอแก้หรือลบข้อมูล">
          <p>
            นักเรียนหรือผู้ปกครองที่ต้องการแก้ไขหรือลบข้อมูล ติดต่อครูผู้สอนได้โดยตรง ครูลบข้อมูลออกจากระบบได้เอง
            และเมื่อจบปีการศึกษาครูลบทั้งภาคเรียนพร้อมคะแนนทั้งหมดได้
          </p>
        </Section>

        <Link to={routes.login} className="mt-6 inline-block text-sm underline underline-offset-2">
          ← กลับหน้าเข้าสู่ระบบ
        </Link>
      </main>
      <Credit className="mx-auto mt-4 block w-fit" />
    </div>
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="mt-6">
      <h2 className="font-medium">{title}</h2>
      <div className="mt-2 grid gap-2 text-sm">{children}</div>
    </section>
  )
}

function Item({ label, children }: { label: string; children: ReactNode }) {
  return (
    <p>
      <b className="font-medium">{label}</b> {children}
    </p>
  )
}
