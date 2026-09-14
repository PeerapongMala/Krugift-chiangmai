import type { UseQueryResult } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { Mascot } from '@/components/Mascot'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { errorMessage } from '@/lib/api'

/**
 * ครอบสถานะ loading / error / ไม่มีข้อมูล ของ TanStack Query ไว้ที่เดียว
 * ใช้ render prop เพื่อให้ children ได้ data ที่การันตีแล้วว่าไม่ undefined
 */
export function QueryState<T>({
  query,
  empty = 'ยังไม่มีข้อมูล',
  children,
}: {
  query: UseQueryResult<T>
  empty?: ReactNode
  children: (data: T) => ReactNode
}) {
  if (query.isPending) {
    return <p className="py-10 text-center text-sm text-muted-foreground">กำลังโหลด...</p>
  }

  if (query.isError) {
    const message = errorMessage(query.error)
    return (
      <Alert variant="destructive">
        <AlertDescription>{message}</AlertDescription>
      </Alert>
    )
  }

  const isEmpty = query.data === undefined || (Array.isArray(query.data) && query.data.length === 0)
  if (isEmpty) {
    return (
      <div className="grid justify-items-center gap-3 py-10 text-center">
        <Mascot name="sleeping" className="h-20 w-auto opacity-90" />
        <p className="text-sm text-muted-foreground">{empty}</p>
      </div>
    )
  }

  return <>{children(query.data)}</>
}
