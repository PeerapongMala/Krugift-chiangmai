import { useQuery } from '@tanstack/react-query'
import { api, ApiError } from '@/lib/api'

/** pending = ล็อกอิน Google แล้วแต่ยังไม่ได้ผูกกับนักเรียน */
export type Role = 'teacher' | 'student' | 'pending'
export type Me = { role: Role; name: string }

export function useMe() {
  return useQuery({
    queryKey: ['me'],
    queryFn: () =>
      api<Me>('/auth/me').catch((e) => {
        if (e instanceof ApiError && e.status === 401) return null
        throw e
      }),
    staleTime: 5 * 60_000,
  })
}

export const homeOf = (role: Role) => (role === 'pending' ? '/claim' : `/${role}`)
