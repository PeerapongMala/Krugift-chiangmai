import { useQuery } from '@tanstack/react-query'
import { api, ApiError } from '@/lib/api'
import { qk, routes } from '@/lib/keys'

/** pending = ล็อกอิน Google แล้วแต่ยังไม่ได้ผูกกับนักเรียน */
export type Role = 'teacher' | 'student' | 'pending'
export type Me = { role: Role; name: string }

export function useMe() {
  return useQuery({
    queryKey: qk.me,
    queryFn: () =>
      api<Me>('/auth/me').catch((e) => {
        if (e instanceof ApiError && e.status === 401) return null
        throw e
      }),
    staleTime: 5 * 60_000,
  })
}

const HOME: Record<Role, string> = {
  teacher: routes.teacher,
  student: routes.student,
  pending: routes.claim,
}

export const homeOf = (role: Role) => HOME[role]
