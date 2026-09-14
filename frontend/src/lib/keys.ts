/** ค่ากลางของเส้นทางและ query key — แก้ที่นี่ที่เดียว ห้ามพิมพ์สตริงซ้ำตามไฟล์ */

export const routes = {
  login: '/login',
  claim: '/claim',
  teacher: '/teacher',
  student: '/student',
} as const

/**
 * query key ของ TanStack Query
 * ใช้ผ่านตัวนี้เสมอ เพราะ invalidateQueries เทียบ key แบบ prefix
 * เช่น invalidate qk.classroom(3) จะล้าง students/items/scores ของห้องนั้นให้ด้วย
 */
export const qk = {
  me: ['me'] as const,

  terms: ['terms'] as const,
  term: (termId: number) => ['terms', termId] as const,

  classrooms: (termId: number) => ['terms', termId, 'classrooms'] as const,
  classroom: (classroomId: number) => ['classrooms', classroomId] as const,
  students: (classroomId: number) => ['classrooms', classroomId, 'students'] as const,
  items: (classroomId: number) => ['classrooms', classroomId, 'items'] as const,
  scores: (classroomId: number) => ['classrooms', classroomId, 'scores'] as const,

  appeals: ['appeals'] as const,
  appeal: (appealId: number) => ['appeals', appealId] as const,
} as const
