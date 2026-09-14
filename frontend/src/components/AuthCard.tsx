import type { ReactNode } from 'react'
import { Credit } from '@/components/Credit'
import { Mascot } from '@/components/Mascot'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function AuthCard({ title, description, children }: { title: string; description: ReactNode; children: ReactNode }) {
  return (
    <div className="auth-bg relative flex min-h-svh flex-col items-center justify-center gap-4 px-4 pt-8 pb-14">
      <Card className="w-full max-w-sm">
        {/* CardHeader เป็น grid จัดกลางแนวนอนต้องใช้ justify-items-center ไม่ใช่ items-center */}
        <CardHeader className="justify-items-center gap-2 text-center">
          <Mascot name="orange" priority className="h-28 w-auto" />
          <CardTitle className="text-xl">{title}</CardTitle>
          <CardDescription>{description}</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-6">{children}</CardContent>
      </Card>
      <Credit className="absolute bottom-3 left-4" />
    </div>
  )
}
