import type { ReactNode } from 'react'
import { Capybara } from '@/components/Capybara'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function AuthCard({ title, description, children }: { title: string; description: ReactNode; children: ReactNode }) {
  return (
    <div className="auth-bg flex min-h-svh items-center justify-center px-4 py-8">
      <Card className="w-full max-w-sm">
        {/* CardHeader เป็น grid — จัดกลางแนวนอนต้องใช้ justify-items-center ไม่ใช่ items-center */}
        <CardHeader className="justify-items-center gap-2 text-center">
          <span className="flex size-20 items-center justify-center rounded-full bg-accent">
            <Capybara className="size-14" />
          </span>
          <CardTitle className="text-xl">{title}</CardTitle>
          <CardDescription>{description}</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-6">{children}</CardContent>
      </Card>
    </div>
  )
}
