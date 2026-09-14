import { Alert, AlertDescription } from '@/components/ui/alert'

/** กล่อง error ของฟอร์ม ไม่ส่ง message มาก็ไม่เรนเดอร์อะไร ผู้เรียกจึงไม่ต้องเขียนเงื่อนไขเอง */
export function FormError({ message }: { message?: string }) {
  if (!message) return null

  return (
    <Alert variant="destructive">
      <AlertDescription>{message}</AlertDescription>
    </Alert>
  )
}
