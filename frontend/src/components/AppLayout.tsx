import { useQuery, useQueryClient } from '@tanstack/react-query'
import { BookOpen, GraduationCap, LogOut, MessagesSquare, Users, type LucideIcon } from 'lucide-react'
import { Link, Outlet, useLocation, useNavigate } from 'react-router'
import { Avatar } from '@/components/Avatar'
import { Credit } from '@/components/Credit'
import { Mascot } from '@/components/Mascot'
import { Button } from '@/components/ui/button'
import { api } from '@/lib/api'
import { useMe, type Me } from '@/lib/auth'
import { qk, routes } from '@/lib/keys'
import { cn } from '@/lib/utils'

type NavItem = {
  to: string
  label: string
  icon: LucideIcon
  isActive: (path: string) => boolean
  /** แสดง badge จำนวนคำถามที่มีข้อความใหม่ */
  showsUnread?: boolean
}

/** v1 ไม่มี realtime ใช้ถาม server ซ้ำทุก 1 นาทีแทน */
const UNREAD_POLL_MS = 60_000

/** เมนูหลักตามบทบาท */
function navFor(me: Me | null | undefined): NavItem[] {
  if (me?.role === 'teacher') {
    const items: NavItem[] = [
      {
        to: routes.teacher,
        label: 'ภาคเรียน',
        icon: BookOpen,
        isActive: (p) => p.startsWith(routes.teacher) && !p.startsWith(routes.staff) && !p.startsWith(routes.teacherAppeals),
      },
      {
        to: routes.teacherAppeals,
        label: 'สอบถามคะแนน',
        icon: MessagesSquare,
        showsUnread: true,
        isActive: (p) => p.startsWith(routes.teacherAppeals),
      },
    ]
    if (me.isOwner) items.push({ to: routes.staff, label: 'จัดการครู', icon: Users, isActive: (p) => p.startsWith(routes.staff) })
    return items
  }
  if (me?.role === 'student') {
    return [
      {
        to: routes.student,
        label: 'คะแนนของฉัน',
        icon: GraduationCap,
        isActive: (p) => p.startsWith(routes.student) && !p.startsWith(routes.studentAppeals),
      },
      {
        to: routes.studentAppeals,
        label: 'สอบถามคะแนน',
        icon: MessagesSquare,
        showsUnread: true,
        isActive: (p) => p.startsWith(routes.studentAppeals),
      },
    ]
  }
  return []
}

function UnreadBadge({ count }: { count: number }) {
  if (count <= 0) return null
  return (
    <span
      className="ml-1 inline-flex min-w-4 items-center justify-center rounded-full bg-primary px-1 text-[10px] leading-4 font-medium text-primary-foreground"
      aria-label={`มีข้อความใหม่ ${count} เรื่อง`}
    >
      {count > 99 ? '99+' : count}
    </span>
  )
}

/**
 * โครงหน้าหลังล็อกอิน
 * - จอ >= 768px (iPad แนวตั้งขึ้นไป / คอม): เมนูอยู่บน header
 * - มือถือ: เมนูเป็น tab bar ด้านล่าง นิ้วโป้งกดถึง · โผล่เฉพาะเมื่อมีมากกว่า 1 เมนู
 */
export function AppLayout() {
  const { data: me } = useMe()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const nav = navFor(me)
  const hasBottomBar = nav.length > 1
  const unread = useQuery({
    queryKey: qk.appealsUnread,
    queryFn: () => api<{ count: number }>('/appeals/unread-count'),
    enabled: nav.length > 0,
    refetchInterval: UNREAD_POLL_MS,
  })
  const unreadCount = unread.data?.count ?? 0

  async function logout() {
    await api('/auth/logout', { method: 'POST' })
    queryClient.clear()
    navigate(routes.login, { replace: true })
  }

  return (
    <div className="flex min-h-svh flex-col">
      <header className="sticky top-0 z-20 border-b bg-card/95 backdrop-blur">
        <div className="mx-auto flex max-w-5xl items-center gap-4 px-4 py-2.5">
          <Link to={nav[0]?.to ?? routes.login} className="flex min-w-0 shrink-0 items-center gap-2 font-semibold">
            <Mascot name="orange" priority className="h-9 w-auto shrink-0" />
            <span className="truncate">Math Krugift</span>
          </Link>

          <nav aria-label="เมนูหลัก" className="hidden flex-1 items-center gap-1 md:flex">
            {nav.map((item) => {
              const active = item.isActive(pathname)
              return (
                <Link
                  key={item.to}
                  to={item.to}
                  aria-current={active ? 'page' : undefined}
                  className={cn(
                    'rounded-md px-3 py-2 text-sm',
                    active ? 'bg-accent font-medium text-accent-foreground' : 'text-muted-foreground hover:text-foreground',
                  )}
                >
                  {item.label}
                  {item.showsUnread && <UnreadBadge count={unreadCount} />}
                </Link>
              )
            })}
          </nav>

          <div className="ml-auto flex min-w-0 items-center gap-2">
            <Avatar />
            <span className="hidden max-w-40 truncate text-sm text-muted-foreground md:inline">{me?.name}</span>
            <Button variant="outline" size="sm" onClick={logout} aria-label="ออกจากระบบ" title="ออกจากระบบ">
              <LogOut />
              <span className="hidden md:inline">ออกจากระบบ</span>
            </Button>
          </div>
        </div>
      </header>

      <main className={cn('mx-auto w-full max-w-5xl flex-1 px-4 py-6', hasBottomBar && 'pb-24 md:pb-6')}>
        <Outlet />
      </main>

      <footer className={cn('px-4 pt-2 pb-4', hasBottomBar && 'pb-20 md:pb-4')}>
        <Credit />
      </footer>

      {hasBottomBar && (
        <nav
          aria-label="เมนูหลัก"
          className="fixed inset-x-0 bottom-0 z-20 border-t bg-card/95 pb-[env(safe-area-inset-bottom)] backdrop-blur md:hidden"
        >
          <div className="grid" style={{ gridTemplateColumns: `repeat(${nav.length}, minmax(0, 1fr))` }}>
            {nav.map((item) => {
              const active = item.isActive(pathname)
              const Icon = item.icon
              return (
                <Link
                  key={item.to}
                  to={item.to}
                  aria-current={active ? 'page' : undefined}
                  className={cn(
                    'flex min-h-14 flex-col items-center justify-center gap-0.5 text-xs',
                    active ? 'font-medium text-primary' : 'text-muted-foreground',
                  )}
                >
                  <Icon className="size-5" aria-hidden="true" />
                  <span>
                    {item.label}
                    {item.showsUnread && <UnreadBadge count={unreadCount} />}
                  </span>
                </Link>
              )
            })}
          </div>
        </nav>
      )}
    </div>
  )
}
