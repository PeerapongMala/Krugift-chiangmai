import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router'
import './index.css'
import { AppLayout } from '@/components/AppLayout'
import { RequireRole } from '@/components/RequireRole'
import { RouteError } from '@/components/RouteError'
import AppealThread from '@/pages/AppealThread'
import Appeals from '@/pages/Appeals'
import Claim from '@/pages/Claim'
import Login from '@/pages/Login'
import QuickScores from '@/pages/QuickScores'
import Classrooms from '@/pages/Classrooms'
import StaffPage from '@/pages/Staff'
import ImportBook from '@/pages/ImportBook'
import ImportExcel from '@/pages/ImportExcel'
import Items from '@/pages/Items'
import Scores from '@/pages/Scores'
import StudentHome from '@/pages/StudentHome'
import Students from '@/pages/Students'
import Terms from '@/pages/Terms'

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: false } },
})

const router = createBrowserRouter([
  {
    // ครอบทุกหน้าไว้ชั้นเดียว หน้าไหนพังก็ขึ้นหน้า error ภาษาไทยแทนหน้าภาษาอังกฤษของ react-router
    errorElement: <RouteError />,
    children: [
      { path: '/login', element: <Login /> },
      { path: '/claim', element: <Claim /> },
      // สาธารณะ ไม่ต้องล็อกอิน
      { path: '/scores', element: <QuickScores /> },
      {
        element: <RequireRole role="teacher" />,
        children: [
          {
            element: <AppLayout />,
            children: [
              { path: '/teacher', element: <Terms /> },
              { path: '/teacher/terms/:termId', element: <Classrooms /> },
              { path: '/teacher/terms/:termId/import', element: <ImportBook /> },
              { path: '/teacher/classrooms/:classroomId', element: <Students /> },
              { path: '/teacher/classrooms/:classroomId/items', element: <Items /> },
              { path: '/teacher/classrooms/:classroomId/scores', element: <Scores /> },
              { path: '/teacher/classrooms/:classroomId/import/:kind', element: <ImportExcel /> },
              { path: '/teacher/staff', element: <StaffPage /> },
              { path: '/teacher/appeals', element: <Appeals /> },
              { path: '/teacher/appeals/:appealId', element: <AppealThread /> },
            ],
          },
        ],
      },
      {
        element: <RequireRole role="student" />,
        children: [
          {
            element: <AppLayout />,
            children: [
              { path: '/student', element: <StudentHome /> },
              { path: '/student/appeals', element: <Appeals /> },
              { path: '/student/appeals/:appealId', element: <AppealThread /> },
            ],
          },
        ],
      },
      { path: '*', element: <Navigate to="/login" replace /> },
    ],
  },
])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
)
