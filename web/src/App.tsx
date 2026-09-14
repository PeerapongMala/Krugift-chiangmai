import { useEffect, useState } from 'react'
import { Button } from '@/components/ui/button'

function App() {
  const [status, setStatus] = useState('กำลังเช็ค API...')

  useEffect(() => {
    fetch('/api/health')
      .then((r) => (r.ok ? r.json() : Promise.reject(r.status)))
      .then((d: { status: string }) => setStatus(`API: ${d.status}`))
      .catch(() => setStatus('API: ติดต่อไม่ได้'))
  }, [])

  return (
    <main className="mx-auto flex min-h-svh max-w-md flex-col items-center justify-center gap-4 px-4">
      <h1 className="text-2xl font-semibold">Krugift คะแนนคณิต</h1>
      <p className="text-muted-foreground">{status}</p>
      <Button onClick={() => location.reload()}>ลองใหม่</Button>
    </main>
  )
}

export default App
