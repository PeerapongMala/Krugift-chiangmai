import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router'
import './index.css'
import { AppLayout } from '@/components/AppLayout'
import { RequireRole } from '@/components/RequireRole'
import Claim from '@/pages/Claim'
import Login from '@/pages/Login'
import Classrooms from '@/pages/Classrooms'
import StaffPage from '@/pages/Staff'
import Terms from '@/pages/Terms'

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: false } },
})

const router = createBrowserRouter([
  { path: '/login', element: <Login /> },
  { path: '/claim', element: <Claim /> },
  {
    element: <RequireRole role="teacher" />,
    children: [
      {
        element: <AppLayout />,
        // ponytail: หน้าชั่วคราว จะแทนด้วยหน้าจริงใน milestone ครู
        children: [
          { path: '/teacher', element: <Terms /> },
          { path: '/teacher/terms/:termId', element: <Classrooms /> },
          { path: '/teacher/staff', element: <StaffPage /> },
        ],
      },
    ],
  },
  {
    element: <RequireRole role="student" />,
    children: [
      {
        element: <AppLayout />,
        children: [{ path: '/student', element: <p>หน้านักเรียน — กำลังทำ</p> }],
      },
    ],
  },
  { path: '*', element: <Navigate to="/login" replace /> },
])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
)
