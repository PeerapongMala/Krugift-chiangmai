import { Navigate, Outlet } from 'react-router'
import { homeOf, useMe, type Role } from '@/lib/auth'

// ป้องกันแค่ฝั่งหน้าเว็บ server เช็คสิทธิ์ซ้ำทุก endpoint อยู่แล้ว
export function RequireRole({ role }: { role: Role }) {
  const { data: me, isPending } = useMe()

  if (isPending) return <p className="p-6 text-muted-foreground">กำลังโหลด...</p>
  if (!me) return <Navigate to="/login" replace />
  if (me.role !== role) return <Navigate to={homeOf(me.role)} replace />
  return <Outlet />
}
