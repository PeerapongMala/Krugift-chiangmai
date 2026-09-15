/** ค่ากลางของเส้นทางและ query key — แก้ที่นี่ที่เดียว ห้ามพิมพ์สตริงซ้ำตามไฟล์ */

export const routes = {
  login: '/login',
  claim: '/claim',
  quickScores: '/scores',
  teacher: '/teacher',
  staff: '/teacher/staff',
  term: (termId: number) => `/teacher/terms/${termId}`,
  classroom: (classroomId: number) => `/teacher/classrooms/${classroomId}`,
  classroomItems: (classroomId: number) => `/teacher/classrooms/${classroomId}/items`,
  classroomScores: (classroomId: number) => `/teacher/classrooms/${classroomId}/scores`,
  /** นำเข้า Excel แยก 2 แบบ: รายชื่อนักเรียน กับ คะแนน */
  classroomImport: (classroomId: number, kind: 'students' | 'scores') =>
    `/teacher/classrooms/${classroomId}/import/${kind}`,
  student: '/student',
  teacherAppeals: '/teacher/appeals',
  studentAppeals: '/student/appeals',
  /** thread ใช้หน้าเดียวกันทั้งสองฝั่ง แต่อยู่ใต้ layout ของแต่ละบทบาท */
  appeal: (base: string, appealId: number) => `${base}/${appealId}`,
} as const

/**
 * query key ของ TanStack Query
 * ใช้ผ่านตัวนี้เสมอ เพราะ invalidateQueries เทียบ key แบบ prefix
 * เช่น invalidate qk.classroom(3) จะล้าง students/items/scores ของห้องนั้นให้ด้วย
 */
export const qk = {
  me: ['me'] as const,
  myScores: ['my-scores'] as const,

  staff: ['staff'] as const,

  terms: ['terms'] as const,
  term: (termId: number) => ['terms', termId] as const,

  classrooms: (termId: number) => ['terms', termId, 'classrooms'] as const,
  classroom: (classroomId: number) => ['classrooms', classroomId] as const,
  students: (classroomId: number) => ['classrooms', classroomId, 'students'] as const,
  items: (classroomId: number) => ['classrooms', classroomId, 'items'] as const,
  scores: (classroomId: number) => ['classrooms', classroomId, 'scores'] as const,

  appeals: ['appeals'] as const,
  /** ขึ้นต้นด้วย 'appeals' เหมือนกัน invalidate qk.appeals แล้ว badge จะอัปเดตตาม */
  appealsUnread: ['appeals', 'unread'] as const,
  appeal: (appealId: number) => ['appeals', appealId] as const,
} as const
