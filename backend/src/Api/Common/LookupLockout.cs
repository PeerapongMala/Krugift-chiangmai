using System.Collections.Concurrent;

namespace Api.Common;

/// <summary>
/// กันคนไล่เดารหัสนักเรียนบนหน้าดูคะแนนด่วน โดย "นับเฉพาะครั้งที่กรอกผิด"
///
/// ทำไมไม่ใช้ rate limit ธรรมดา: ทั้งโรงเรียนออกเน็ตด้วย IP เดียวกัน (NAT)
/// ถ้านับทุกคำขอ เด็กห้องเดียวกันเปิดดูพร้อมกัน 45 คน คนที่ 11 เป็นต้นไปจะโดนบล็อกทั้งที่กรอกถูก
/// คนที่ไล่เดารหัสยังโดนบล็อกเหมือนเดิม เพราะการเดาคือการกรอกผิดซ้ำ ๆ
///
/// เก็บใน memory ของ process เดียว พอสำหรับ Render 1 instance · รีสตาร์ทแล้วเริ่มนับใหม่ ยอมรับได้
/// </summary>
public class LookupLockout
{
    /// กรอกผิดเกินกี่ครั้งใน 1 ช่วงเวลาถึงบล็อก
    public const int MaxFailures = 10;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// กันตารางบวมถ้าโดนยิงจากไอพีมั่ว ๆ จำนวนมาก
    const int PruneAt = 5_000;

    readonly ConcurrentDictionary<string, Attempts> failures = new();

    record Attempts(DateTime Start, int Count);

    public bool IsLocked(string key, DateTime now) =>
        failures.TryGetValue(key, out var a) && now - a.Start < Window && a.Count >= MaxFailures;

    public void RecordFailure(string key, DateTime now)
    {
        failures.AddOrUpdate(key,
            _ => new Attempts(now, 1),
            (_, a) => now - a.Start < Window ? a with { Count = a.Count + 1 } : new Attempts(now, 1));

        if (failures.Count > PruneAt) Prune(now);
    }

    /// กรอกถูกแล้วล้างยอดที่ผิดทิ้ง คนที่พิมพ์รหัสตัวเองพลาด 2-3 ทีจะได้ไม่มียอดค้างสะสม
    public void Clear(string key) => failures.TryRemove(key, out _);

    void Prune(DateTime now)
    {
        foreach (var (key, attempt) in failures)
            if (now - attempt.Start >= Window)
                failures.TryRemove(key, out _);
    }
}
