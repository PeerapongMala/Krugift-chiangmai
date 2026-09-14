import { useQueryClient } from '@tanstack/react-query'
import { Outlet, useNavigate } from 'react-router'
import { Capybara } from '@/components/Capybara'
import { Button } from '@/components/ui/button'
import { api } from '@/lib/api'
import { useMe } from '@/lib/auth'

export function AppLayout() {
  const { data: me } = useMe()
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  async function logout() {
    await api('/auth/logout', { method: 'POST' })
    queryClient.clear()
    navigate('/login', { replace: true })
  }

  return (
    <div className="min-h-svh">
      <header className="border-b bg-card">
        <div className="mx-auto flex max-w-5xl items-center justify-between gap-4 px-4 py-3">
          <span className="flex min-w-0 items-center gap-2 font-semibold">
            <Capybara className="size-7 shrink-0" />
            <span className="truncate">Krugift คะแนนคณิต</span>
          </span>
          <div className="flex min-w-0 items-center gap-3 text-sm">
            <span className="truncate text-muted-foreground">{me?.name}</span>
            <Button variant="outline" size="sm" onClick={logout}>
              ออกจากระบบ
            </Button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-5xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
