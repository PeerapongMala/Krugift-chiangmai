namespace Api.Common;

/// ตัวช่วยสร้าง ProblemDetails ข้อความไทย — api() ฝั่งเว็บจะหยิบ detail ไปแสดงให้ผู้ใช้เอง
/// รวมไว้ที่เดียวเพื่อให้ทั้งระบบพูดเหมือนกัน ไม่ใช่ต่าง endpoint ต่างสำนวน
public static class Problems
{
    public static IResult NotFound(string what) =>
        Results.Problem($"ไม่พบ{what}", statusCode: StatusCodes.Status404NotFound);

    public static IResult Invalid(string message) =>
        Results.Problem(message, statusCode: StatusCodes.Status400BadRequest);

    public static IResult Conflict(string message) =>
        Results.Problem(message, statusCode: StatusCodes.Status409Conflict);

    public static IResult Duplicate(string what) =>
        Conflict($"มี{what}นี้อยู่แล้ว");

    public static IResult Denied(string message = "ไม่มีสิทธิ์เข้าถึงข้อมูลนี้") =>
        Results.Problem(message, statusCode: StatusCodes.Status403Forbidden);
}
