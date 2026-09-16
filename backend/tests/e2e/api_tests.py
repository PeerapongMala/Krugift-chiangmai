# -*- coding: utf-8 -*-
"""
ชุดทดสอบ API แบบ end-to-end + snapshot

ทำไมเป็น Python ไม่ใช่ xUnit: ตัวนี้ยิง HTTP เข้า API ที่รันอยู่จริงพร้อม DB จริง
ส่วน Api.Tests เป็น unit test ของฟังก์ชันบริสุทธิ์ คนละชั้นกัน

ทำไมไม่ใช้ curl: console ของ Windows แปลงอักษรไทยใน argument เป็น ? ก่อนส่ง
ทำให้ไล่บั๊กผิดทาง ตัวนี้ใช้ urllib + bytes ตรง ๆ จึงปลอดภัย

วิธีรัน (ต้องมี API รันที่ localhost:5080 ก่อน)
    python backend/tests/e2e/api_tests.py              เทียบกับ snapshot
    python backend/tests/e2e/api_tests.py --update     บันทึก snapshot ใหม่
    python backend/tests/e2e/api_tests.py --ratelimit  รวมเทสต์ rate limit ด้วย
                                                       (ไม่อยู่ใน snapshot เพราะผลขึ้นกับเวลา)

snapshot เก็บทั้ง status และ "ข้อความไทยที่ผู้ใช้เห็น" จึงจับได้ทันทีถ้ามีใครเผลอแก้ copy
"""
import http.cookiejar
import io
import json
import os
import sys
import time
import urllib.error
import urllib.request

BASE = os.environ.get("KRUGIFT_API", "http://localhost:5080")
OWNER = os.environ.get("KRUGIFT_OWNER", "areeraktuyla16842@gmail.com")
HERE = os.path.dirname(os.path.abspath(__file__))
SNAPSHOT = os.path.join(HERE, "snapshot.txt")

lines = []


class Client:
    """ผู้ใช้หนึ่งคน (มี cookie jar ของตัวเอง)"""

    def __init__(self):
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.jar))

    def __call__(self, method, path, body=None, raw=None):
        data = raw if raw is not None else (
            json.dumps(body, ensure_ascii=False).encode("utf-8") if body is not None else None)
        req = urllib.request.Request(BASE + path, data=data, method=method)
        if data is not None:
            req.add_header("Content-Type", "application/json; charset=utf-8")
        try:
            with self.opener.open(req) as r:
                payload = r.read()
                return r.status, (json.loads(payload.decode("utf-8")) if payload else None)
        except urllib.error.HTTPError as e:
            payload = e.read()
            try:
                return e.code, (json.loads(payload.decode("utf-8")) if payload else None)
            except Exception:
                return e.code, None


def record(group, name, status, body=None):
    """บันทึกผลหนึ่งเคส · เก็บข้อความไทยด้วยเพื่อล็อก copy ที่ผู้ใช้เห็น"""
    detail = body.get("detail", "") if isinstance(body, dict) else ""
    lines.append("%s|%s|%s|%s" % (group, name, status, detail))


def value(group, name, got):
    """บันทึกค่าที่อ่านกลับมา ใช้ยืนยันว่าข้อมูลเปลี่ยนจริงไม่ใช่แค่ตอบ 200"""
    lines.append("%s|%s|=|%s" % (group, name, got))


def wait_for_api():
    for _ in range(40):
        try:
            urllib.request.urlopen(BASE + "/api/health", timeout=2)
            return True
        except Exception:
            time.sleep(2)
    return False


def run():
    owner = Client()
    status, _ = owner("POST", "/api/auth/dev-login?email=" + OWNER)
    if status != 200:
        print("ล็อกอินเป็นเจ้าของไม่ได้ (%s) — ตรวจว่า API รันอยู่และมีครูอีเมล %s" % (status, OWNER))
        return False

    anon = Client()

    # เตรียมครูทั่วไปไว้ทดสอบการแยกข้อมูลระหว่างครู
    owner("POST", "/api/staff", {"email": "e2e.teacher@example.com", "role": "Teacher"})
    other = Client()
    other("POST", "/api/auth/dev-login?email=e2e.teacher@example.com")

    # ---------------------------------------------------------------- ไม่ล็อกอิน
    g = "ANON"
    for label, method, path, body in [
        ("ดูภาคเรียน", "GET", "/api/terms", None),
        ("สร้างภาคเรียน", "POST", "/api/terms", {"name": "x"}),
        ("ดูรายชื่อครู", "GET", "/api/staff", None),
        ("แก้คะแนน", "PUT", "/api/scores", {"itemId": 1, "studentId": 1, "value": 1, "expected": None}),
        ("ผูกบัญชีโดยไม่ผ่าน Google", "POST", "/api/auth/claim", {"studentCode": "1"}),
    ]:
        record(g, label, *anon(method, path, body))
    record(g, "ดูสถานะตัวเอง", *anon("GET", "/api/auth/me"))
    record(g, "ดูคะแนนของฉัน", *anon("GET", "/api/me/scores"))

    # ---------------------------------------------------------------- ภาคเรียน
    g = "TERM"
    status, term = owner("POST", "/api/terms", {"name": "E2E ภาคเรียนทดสอบ"})
    record(g, "สร้างภาคเรียน", status)
    term_id = term["id"]
    record(g, "ชื่อซ้ำ (มีช่องว่างหน้าหลัง)", *owner("POST", "/api/terms", {"name": "  E2E ภาคเรียนทดสอบ  "}))
    record(g, "ชื่อว่าง", *owner("POST", "/api/terms", {"name": ""}))
    record(g, "ชื่อเว้นวรรคล้วน", *owner("POST", "/api/terms", {"name": "    "}))
    record(g, "ชื่อยาว 201 ตัว", *owner("POST", "/api/terms", {"name": "ก" * 201}))
    record(g, "ไม่มี field name", *owner("POST", "/api/terms", {"wrong": 1}))
    record(g, "body ไม่ใช่ JSON", *owner("POST", "/api/terms", raw=b"<<<>>>"))
    record(g, "body ว่างเปล่า", *owner("POST", "/api/terms", raw=b""))
    record(g, "เปลี่ยนชื่อภาคเรียนที่ไม่มีอยู่", *owner("PATCH", "/api/terms/999999", {"name": "x"}))
    record(g, "ลบภาคเรียนที่ไม่มีอยู่", *owner("DELETE", "/api/terms/999999"))

    # ---------------------------------------------------------------- ห้องเรียน
    g = "ROOM"
    status, room = owner("POST", "/api/terms/%d/classrooms" % term_id, {"name": "ม.3/1"})
    record(g, "สร้างห้อง", status)
    room_id = room["id"]
    status, room2 = owner("POST", "/api/terms/%d/classrooms" % term_id, {"name": "ม.3/2"})
    room2_id = room2["id"]
    record(g, "ชื่อห้องซ้ำ", *owner("POST", "/api/terms/%d/classrooms" % term_id, {"name": "ม.3/1"}))
    record(g, "ชื่อห้องว่าง", *owner("POST", "/api/terms/%d/classrooms" % term_id, {"name": " "}))
    record(g, "เพิ่มห้องในภาคเรียนที่ไม่มีอยู่", *owner("POST", "/api/terms/999999/classrooms", {"name": "x"}))
    record(g, "ลบภาคเรียนที่ยังมีห้อง", *owner("DELETE", "/api/terms/%d" % term_id))
    record(g, "แก้ห้องที่ไม่มีอยู่", *owner("PATCH", "/api/classrooms/999999", {"name": "x"}))

    # ---------------------------------------------------------------- นักเรียน
    g = "STUDENT"
    status, s1 = owner("POST", "/api/classrooms/%d/students" % room_id,
                       {"studentCode": "90001", "firstName": "เอ", "lastName": "หนึ่ง", "no": 1})
    record(g, "เพิ่มนักเรียน", status)
    sid1 = s1["studentId"]
    value(g, "  ชื่อที่อ่านกลับมา", s1["firstName"])
    value(g, "  ยังไม่ผูก Google", s1["hasGoogle"])

    status, s2 = owner("POST", "/api/classrooms/%d/students" % room_id,
                       {"studentCode": "90002", "firstName": "บี", "lastName": "สอง", "no": 2})
    sid2 = s2["studentId"]
    record(g, "เลขที่ขอบบน 999", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                        {"studentCode": "90003", "firstName": "ซี", "lastName": "สาม", "no": 999}))
    record(g, "รหัสยาว 20 ตัว", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                       {"studentCode": "A" * 20, "firstName": "ดี", "lastName": "สี่", "no": 4}))
    record(g, "รหัสยาว 21 ตัว", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                       {"studentCode": "A" * 21, "firstName": "ก", "lastName": "ข", "no": 5}))
    record(g, "รหัสว่าง", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                 {"studentCode": "", "firstName": "ก", "lastName": "ข", "no": 6}))
    record(g, "รหัสมีช่องว่างกลาง", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                           {"studentCode": "900 04", "firstName": "ก", "lastName": "ข", "no": 7}))
    record(g, "รหัสมีอักขระพิเศษ", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                          {"studentCode": "900#4", "firstName": "ก", "lastName": "ข", "no": 8}))
    record(g, "เลขที่ 0", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                 {"studentCode": "90010", "firstName": "ก", "lastName": "ข", "no": 0}))
    record(g, "เลขที่ 1000", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                    {"studentCode": "90011", "firstName": "ก", "lastName": "ข", "no": 1000}))
    record(g, "เลขที่ติดลบ", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                    {"studentCode": "90012", "firstName": "ก", "lastName": "ข", "no": -1}))
    record(g, "ชื่อว่าง", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                 {"studentCode": "90013", "firstName": "  ", "lastName": "ข", "no": 9}))
    record(g, "นามสกุลว่าง", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                    {"studentCode": "90014", "firstName": "ก", "lastName": "", "no": 10}))
    record(g, "เลขที่ซ้ำในห้อง", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                        {"studentCode": "90015", "firstName": "ก", "lastName": "ข", "no": 1}))
    record(g, "รหัสซ้ำในห้องเดิม", *owner("POST", "/api/classrooms/%d/students" % room_id,
                                          {"studentCode": "90001", "firstName": "ก", "lastName": "ข", "no": 11}))
    record(g, "ห้องที่ไม่มีอยู่", *owner("POST", "/api/classrooms/999999/students",
                                         {"studentCode": "90016", "firstName": "ก", "lastName": "ข", "no": 1}))

    record(g, "แก้ชื่อและเลขที่", *owner("PATCH", "/api/classrooms/%d/students/%d" % (room_id, sid1),
                                         {"firstName": "เอใหม่", "lastName": "หนึ่ง", "no": 20}))
    status, roster = owner("GET", "/api/classrooms/%d/students" % room_id)
    changed = next(x for x in roster if x["studentId"] == sid1)
    value(g, "  ชื่อเปลี่ยนจริง", changed["firstName"])
    value(g, "  เลขที่เปลี่ยนจริง", changed["no"])
    value(g, "  เรียงตามเลขที่", [x["no"] for x in roster] == sorted(x["no"] for x in roster))
    record(g, "แก้ให้ชื่อว่าง", *owner("PATCH", "/api/classrooms/%d/students/%d" % (room_id, sid1),
                                       {"firstName": "", "lastName": "ข", "no": 20}))
    record(g, "แก้ให้เลขที่ชนคนอื่น", *owner("PATCH", "/api/classrooms/%d/students/%d" % (room_id, sid1),
                                             {"firstName": "ก", "lastName": "ข", "no": 2}))
    record(g, "แก้นักเรียนที่ไม่มีอยู่", *owner("PATCH", "/api/classrooms/%d/students/999999" % room_id,
                                                {"firstName": "ก", "lastName": "ข", "no": 1}))
    record(g, "แก้นักเรียนที่ไม่ได้อยู่ห้องนี้", *owner("PATCH", "/api/classrooms/%d/students/%d" % (room2_id, sid2),
                                                        {"firstName": "ก", "lastName": "ข", "no": 1}))

    status, same = owner("POST", "/api/classrooms/%d/students" % room2_id,
                         {"studentCode": "90001", "firstName": "ไม่สน", "lastName": "ไม่สน", "no": 1})
    record(g, "รหัสเดิมเข้าอีกห้อง", status)
    value(g, "  ต้องเป็นคนเดิม", same["studentId"] == sid1)
    value(g, "  ใช้ชื่อเดิมไม่ทับ", same["firstName"])

    record(g, "ยกเลิกการผูกทั้งที่ยังไม่ผูก", *owner("POST", "/api/students/%d/unlink" % sid1))
    record(g, "ยกเลิกการผูกคนที่ไม่มีอยู่", *owner("POST", "/api/students/999999/unlink"))

    # ---------------------------------------------------------------- รายการคะแนน
    g = "ITEM"
    status, item = owner("POST", "/api/classrooms/%d/items" % room_id, {"name": "สอบกลางภาค", "maxScore": 20})
    record(g, "สร้างรายการ", status)
    item_id = item["id"]
    value(g, "  sortOrder เริ่มที่ 1", item["sortOrder"])
    status, item2 = owner("POST", "/api/classrooms/%d/items" % room_id, {"name": "เก็บคะแนน", "maxScore": 10})
    value(g, "  รายการที่สองได้ order 2", item2["sortOrder"])
    record(g, "ชื่อรายการซ้ำ", *owner("POST", "/api/classrooms/%d/items" % room_id,
                                      {"name": " สอบกลางภาค ", "maxScore": 5}))
    record(g, "ชื่อรายการว่าง", *owner("POST", "/api/classrooms/%d/items" % room_id, {"name": " ", "maxScore": 5}))
    record(g, "คะแนนเต็ม 0", *owner("POST", "/api/classrooms/%d/items" % room_id, {"name": "a", "maxScore": 0}))
    record(g, "คะแนนเต็มติดลบ", *owner("POST", "/api/classrooms/%d/items" % room_id, {"name": "b", "maxScore": -1}))
    record(g, "คะแนนเต็มทศนิยมเกิน 2 ตำแหน่ง", *owner("POST", "/api/classrooms/%d/items" % room_id,
                                                   {"name": "c", "maxScore": 10.555}))
    record(g, "คะแนนเต็มเกินเพดาน", *owner("POST", "/api/classrooms/%d/items" % room_id,
                                           {"name": "c", "maxScore": 10000}))
    record(g, "รายการในห้องที่ไม่มีอยู่", *owner("POST", "/api/classrooms/999999/items",
                                                 {"name": "d", "maxScore": 5}))
    record(g, "แก้รายการที่ไม่มีอยู่", *owner("PATCH", "/api/items/999999", {"name": "x", "maxScore": 5}))

    # ---------------------------------------------------------------- คะแนน
    g = "SCORE"
    record(g, "กรอกครั้งแรก", *owner("PUT", "/api/scores",
                                     {"itemId": item_id, "studentId": sid1, "value": 15.5, "expected": None}))
    record(g, "แก้ตามค่าที่เห็นบนจอ", *owner("PUT", "/api/scores",
                                             {"itemId": item_id, "studentId": sid1, "value": 18, "expected": 15.5}))
    record(g, "เท่าคะแนนเต็มพอดี", *owner("PUT", "/api/scores",
                                          {"itemId": item_id, "studentId": sid2, "value": 20, "expected": None}))
    record(g, "เกินคะแนนเต็ม", *owner("PUT", "/api/scores",
                                      {"itemId": item_id, "studentId": sid1, "value": 20.01, "expected": 18}))
    record(g, "ติดลบ", *owner("PUT", "/api/scores",
                              {"itemId": item_id, "studentId": sid1, "value": -0.5, "expected": 18}))
    record(g, "ทศนิยมเกิน 2 ตำแหน่ง", *owner("PUT", "/api/scores",
                                           {"itemId": item_id, "studentId": sid1, "value": 18.555, "expected": 18}))
    record(g, "*** ชนกัน ค่าที่เห็นไม่ตรงกับใน DB", *owner("PUT", "/api/scores",
                                                           {"itemId": item_id, "studentId": sid1,
                                                            "value": 5, "expected": 99}))
    record(g, "*** อ้างว่าช่องว่างทั้งที่มีค่าแล้ว", *owner("PUT", "/api/scores",
                                                            {"itemId": item_id, "studentId": sid1,
                                                             "value": 5, "expected": None}))
    record(g, "นักเรียนไม่ได้อยู่ห้องนี้", *owner("PUT", "/api/scores",
                                                  {"itemId": item_id, "studentId": 999999,
                                                   "value": 5, "expected": None}))
    record(g, "รายการที่ไม่มีอยู่", *owner("PUT", "/api/scores",
                                           {"itemId": 999999, "studentId": sid1, "value": 5, "expected": None}))
    record(g, "ล้างคะแนนเป็นว่าง", *owner("PUT", "/api/scores",
                                          {"itemId": item_id, "studentId": sid2, "value": None, "expected": 20}))

    status, grid = owner("GET", "/api/classrooms/%d/scores" % room_id)
    record(g, "ดึงตารางทั้งห้อง", status)
    value(g, "  จำนวนรายการ", len(grid["items"]))
    value(g, "  จำนวนนักเรียน", len(grid["students"]))
    value(g, "  ช่องที่มีคะแนน", len(grid["scores"]))
    value(g, "  ค่าที่อ่านกลับมา", float(grid["scores"][0]["value"]))

    # ---------------------------------------------------------------- ประวัติการแก้
    g = "AUDIT"
    status, audits = owner("GET", "/api/scores/audits?itemId=%d&studentId=%d" % (item_id, sid1))
    record(g, "ดึงประวัติ", status)
    value(g, "  จำนวนครั้งที่แก้", len(audits))
    value(g, "  ล่าสุด เก่า -> ใหม่", "%s -> %s" % (audits[0]["oldValue"], audits[0]["newValue"]))
    value(g, "  เรียงใหม่สุดขึ้นก่อน", audits[0]["at"] >= audits[-1]["at"])
    value(g, "  บันทึกว่าใครแก้", bool(audits[0]["by"]))
    status, audits2 = owner("GET", "/api/scores/audits?itemId=%d&studentId=%d" % (item_id, sid2))
    value(g, "  การล้างค่าก็ถูกบันทึก", len(audits2))

    # ---------------------------------------------------------------- กฎธุรกิจ
    g = "RULES"
    record(g, "ลดคะแนนเต็มต่ำกว่าที่กรอกไว้", *owner("PATCH", "/api/items/%d" % item_id,
                                                     {"name": "สอบกลางภาค", "maxScore": 10}))
    record(g, "เพิ่มคะแนนเต็มได้", *owner("PATCH", "/api/items/%d" % item_id,
                                          {"name": "สอบกลางภาค", "maxScore": 25}))
    record(g, "ลบรายการที่มีคะแนนแล้ว", *owner("DELETE", "/api/items/%d" % item_id))
    record(g, "ลบรายการที่ยังไม่มีคะแนน", *owner("DELETE", "/api/items/%d" % item2["id"]))
    record(g, "ลบห้องที่ยังมีรายการ", *owner("DELETE", "/api/classrooms/%d" % room_id))

    # ---------------------------------------------------------------- ครูทั่วไป
    g = "TEACHER"
    record(g, "ดูรายชื่อครู", *other("GET", "/api/staff"))
    record(g, "ครูเรียกหน้าคะแนนของนักเรียน", *other("GET", "/api/me/scores"))
    record(g, "เพิ่มครู", *other("POST", "/api/staff", {"email": "a@b.com", "role": "Teacher"}))
    status, mine = other("GET", "/api/terms")
    record(g, "ดูภาคเรียนของตัวเอง", status)
    value(g, "  ต้องไม่เห็นภาคเรียนของคนอื่น", len(mine))

    g = "IDOR"
    record(g, "แก้ภาคเรียนของครูอื่น", *other("PATCH", "/api/terms/%d" % term_id, {"name": "แอบแก้"}))
    record(g, "ลบภาคเรียนของครูอื่น", *other("DELETE", "/api/terms/%d" % term_id))
    record(g, "ดูห้องของครูอื่น", *other("GET", "/api/terms/%d/classrooms" % term_id))
    record(g, "ดูนักเรียนของครูอื่น", *other("GET", "/api/classrooms/%d/students" % room_id))
    record(g, "เพิ่มนักเรียนในห้องครูอื่น", *other("POST", "/api/classrooms/%d/students" % room_id,
                                                   {"studentCode": "91000", "firstName": "ก", "lastName": "ข", "no": 1}))
    record(g, "แก้นักเรียนของครูอื่น", *other("PATCH", "/api/classrooms/%d/students/%d" % (room_id, sid1),
                                              {"firstName": "แอบแก้", "lastName": "ข", "no": 1}))
    record(g, "ยกเลิกการผูกของครูอื่น", *other("POST", "/api/students/%d/unlink" % sid1))
    record(g, "ดูรายการคะแนนของครูอื่น", *other("GET", "/api/classrooms/%d/items" % room_id))
    record(g, "ดูตารางคะแนนของครูอื่น", *other("GET", "/api/classrooms/%d/scores" % room_id))
    record(g, "แก้คะแนนของครูอื่น", *other("PUT", "/api/scores",
                                           {"itemId": item_id, "studentId": sid1, "value": 1, "expected": 18}))
    record(g, "ดูประวัติของครูอื่น", *other("GET", "/api/scores/audits?itemId=%d&studentId=%d" % (item_id, sid1)))

    # ---------------------------------------------------------------- นักเรียน
    g = "PUPIL"
    pupil = Client()
    status, _ = pupil("POST", "/api/auth/dev-login-student?studentCode=90001")
    record(g, "นักเรียนล็อกอิน", status)
    status, me = pupil("GET", "/api/auth/me")
    value(g, "  role ที่ได้", me["role"])
    value(g, "  ไม่ใช่เจ้าของ", me["isOwner"])
    status, mine = pupil("GET", "/api/me/scores")
    record(g, "นักเรียนดูคะแนนของตัวเอง", status)
    value(g, "  จำนวนห้องที่เห็น", len(mine) if isinstance(mine, list) else None)
    value(g, "  ห้องและคะแนน", [(r["classroom"], r["no"], [(i["name"], i["value"], i["maxScore"]) for i in r["items"]])
                               for r in mine] if isinstance(mine, list) else None)
    record(g, "นักเรียนดูภาคเรียน", *pupil("GET", "/api/terms"))
    record(g, "นักเรียนสร้างภาคเรียน", *pupil("POST", "/api/terms", {"name": "x"}))
    record(g, "นักเรียนดูรายชื่อครู", *pupil("GET", "/api/staff"))
    record(g, "นักเรียนดูนักเรียนในห้อง", *pupil("GET", "/api/classrooms/%d/students" % room_id))
    record(g, "นักเรียนดูตารางคะแนน", *pupil("GET", "/api/classrooms/%d/scores" % room_id))
    record(g, "นักเรียนแก้คะแนนตัวเอง", *pupil("PUT", "/api/scores",
                                               {"itemId": item_id, "studentId": sid1, "value": 25, "expected": 18}))
    record(g, "นักเรียนดูประวัติ", *pupil("GET", "/api/scores/audits?itemId=%d&studentId=%d" % (item_id, sid1)))
    record(g, "นักเรียนลบห้อง", *pupil("DELETE", "/api/classrooms/%d" % room_id))
    record(g, "นักเรียนยกเลิกการผูกตัวเอง", *pupil("POST", "/api/students/%d/unlink" % sid1))
    record(g, "นักเรียนออกจากระบบ", *pupil("POST", "/api/auth/logout"))
    record(g, "ออกแล้วดูภาคเรียนอีก", *pupil("GET", "/api/terms"))

    # ---------------------------------------------------------------- ท้วงคะแนน
    g = "APPEAL"
    stu = Client()
    stu("POST", "/api/auth/dev-login-student?studentCode=90001")
    stu2 = Client()
    stu2("POST", "/api/auth/dev-login-student?studentCode=90002")

    def find(client, aid):
        """แถวของเรื่องท้วงนี้ในรายการที่ client เห็น"""
        rows = client("GET", "/api/appeals")[1]
        return next((r for r in rows if r["id"] == aid), None) if isinstance(rows, list) else None

    record(g, "คนไม่ล็อกอินดูรายการท้วง", *anon("GET", "/api/appeals"))
    record(g, "ครูเปิดเรื่องท้วงเอง", *owner("POST", "/api/appeals", {"itemId": item_id, "body": "ครูลองท้วง"}))
    record(g, "เหตุผลว่าง", *stu("POST", "/api/appeals", {"itemId": item_id, "body": "   "}))
    record(g, "เหตุผลยาวเกิน", *stu("POST", "/api/appeals", {"itemId": item_id, "body": "ก" * 2001}))
    record(g, "รายการที่ไม่มีอยู่", *stu("POST", "/api/appeals", {"itemId": 999999, "body": "ท้วง"}))
    status, opened = stu("POST", "/api/appeals", {"itemId": item_id, "body": "ข้อ 3 ตอบถูกแต่ไม่ได้คะแนน"})
    record(g, "นักเรียนเปิดเรื่องท้วง", status)
    appeal_id = opened["id"] if isinstance(opened, dict) else 0
    record(g, "เปิดซ้ำรายการเดิมที่ยังไม่ปิด", *stu("POST", "/api/appeals", {"itemId": item_id, "body": "อีกรอบ"}))
    value(g, "  นักเรียนเห็นเรื่องตัวเอง", (find(stu, appeal_id) or {}).get("status"))

    record(g, "นักเรียนคนอื่นดูเรื่องนี้", *stu2("GET", "/api/appeals/%d" % appeal_id))
    record(g, "นักเรียนคนอื่นตอบ", *stu2("POST", "/api/appeals/%d/messages" % appeal_id, {"body": "แอบตอบ"}))
    record(g, "นักเรียนคนอื่นปิด", *stu2("PATCH", "/api/appeals/%d/close" % appeal_id))
    value(g, "  นักเรียนคนอื่นไม่เห็นในรายการ", find(stu2, appeal_id) is None)
    record(g, "ครูอื่นดูเรื่องในห้องที่ไม่ใช่ของตัวเอง", *other("GET", "/api/appeals/%d" % appeal_id))
    record(g, "ครูอื่นตอบ", *other("POST", "/api/appeals/%d/messages" % appeal_id, {"body": "แอบตอบ"}))
    value(g, "  ครูอื่นไม่เห็นในรายการ", find(other, appeal_id) is None)

    value(g, "  ครูเห็นว่ามีข้อความใหม่", (find(owner, appeal_id) or {}).get("unread"))
    status, thread = owner("GET", "/api/appeals/%d" % appeal_id)
    record(g, "ครูเปิดอ่าน", status)
    value(g, "  ข้อมูลใน thread", (thread["student"], thread["studentCode"], thread["item"], thread["score"],
                                   len(thread["messages"])) if isinstance(thread, dict) else None)
    value(g, "  เปิดอ่านแล้วล้างสถานะใหม่", (find(owner, appeal_id) or {}).get("unread"))
    record(g, "ครูตอบข้อความว่าง", *owner("POST", "/api/appeals/%d/messages" % appeal_id, {"body": ""}))
    record(g, "ครูตอบ", *owner("POST", "/api/appeals/%d/messages" % appeal_id, {"body": "ครูตรวจแล้ว ข้อ 3 ผิดจริง"}))
    row = find(stu, appeal_id) or {}
    value(g, "  นักเรียนเห็นสถานะหลังครูตอบ", (row.get("status"), row.get("unread")))
    value(g, "  badge ของนักเรียน", (stu("GET", "/api/appeals/unread-count")[1] or {}).get("count"))
    stu("GET", "/api/appeals/%d" % appeal_id)
    value(g, "  badge หลังนักเรียนเปิดอ่าน", (stu("GET", "/api/appeals/unread-count")[1] or {}).get("count"))
    record(g, "นักเรียนตอบกลับ", *stu("POST", "/api/appeals/%d/messages" % appeal_id, {"body": "ขอดูกระดาษคำตอบได้ไหมคะ"}))
    value(g, "  สถานะหลังนักเรียนตอบกลับ", (find(owner, appeal_id) or {}).get("status"))
    record(g, "นักเรียนปิดเรื่อง", *stu("PATCH", "/api/appeals/%d/close" % appeal_id))
    row = find(owner, appeal_id) or {}
    value(g, "  ครูเห็นว่าปิดแล้ว", (row.get("status"), row.get("unread")))
    record(g, "ตอบเรื่องที่ปิดแล้ว", *owner("POST", "/api/appeals/%d/messages" % appeal_id, {"body": "ตอบต่อ"}))
    record(g, "ปิดแล้วเปิดเรื่องใหม่รายการเดิมได้", stu("POST", "/api/appeals", {"itemId": item_id, "body": "ยังไม่เคลียร์"})[0])

    # ---------------------------------------------------------------- นำเข้า Excel (all-or-nothing)
    g = "IMPORT"
    import zipfile
    from xml.sax.saxutils import escape

    def xlsx(rows):
        """สร้าง .xlsx ขั้นต่ำด้วย stdlib (เครื่องนี้ไม่มี openpyxl) · ข้อความเป็น inline string ตัวเลขเป็น number"""
        def cell(ref, v):
            if v is None:
                return ""
            if isinstance(v, (int, float)):
                return '<c r="%s"><v>%s</v></c>' % (ref, v)
            return '<c r="%s" t="inlineStr"><is><t>%s</t></is></c>' % (ref, escape(v))
        sheet_rows = "".join(
            '<row r="%d">%s</row>' % (r + 1, "".join(cell("%s%d" % (chr(65 + c), r + 1), v) for c, v in enumerate(row)))
            for r, row in enumerate(rows))
        main = 'xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"'
        pkg = "http://schemas.openxmlformats.org/package/2006/relationships"
        rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
        sml = "application/vnd.openxmlformats-officedocument.spreadsheetml"
        parts = {
            "[Content_Types].xml":
                '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
                '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
                '<Default Extension="xml" ContentType="application/xml"/>'
                '<Override PartName="/xl/workbook.xml" ContentType="%s.sheet.main+xml"/>'
                '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="%s.worksheet+xml"/>'
                '<Override PartName="/xl/styles.xml" ContentType="%s.styles+xml"/></Types>' % (sml, sml, sml),
            "_rels/.rels":
                '<Relationships xmlns="%s"><Relationship Id="rId1" Type="%s/officeDocument" Target="xl/workbook.xml"/>'
                '</Relationships>' % (pkg, rel),
            "xl/workbook.xml":
                '<workbook %s xmlns:r="%s"><sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets></workbook>'
                % (main, rel),
            "xl/_rels/workbook.xml.rels":
                '<Relationships xmlns="%s"><Relationship Id="rId1" Type="%s/worksheet" Target="worksheets/sheet1.xml"/>'
                '<Relationship Id="rId2" Type="%s/styles" Target="styles.xml"/></Relationships>' % (pkg, rel, rel),
            "xl/styles.xml":
                '<styleSheet %s><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>'
                '<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>'
                '<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>'
                '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>'
                '<cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs></styleSheet>'
                % main,
            "xl/worksheets/sheet1.xml": '<worksheet %s><sheetData>%s</sheetData></worksheet>' % (main, sheet_rows),
        }
        buf = io.BytesIO()
        with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
            for name, xml in parts.items():
                z.writestr(name, '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' + xml)
        return buf.getvalue()

    def upload(client, path, files, fields=()):
        """ส่ง multipart/form-data เอง (urllib ไม่มีให้) · คืน (status, JSON)"""
        boundary = "krugift-e2e-boundary"
        body = io.BytesIO()
        for name, text in fields:
            body.write(('--%s\r\nContent-Disposition: form-data; name="%s"\r\n\r\n%s\r\n'
                        % (boundary, name, text)).encode("utf-8"))
        for name, filename, data in files:
            body.write(('--%s\r\nContent-Disposition: form-data; name="%s"; filename="%s"\r\n'
                        'Content-Type: application/octet-stream\r\n\r\n' % (boundary, name, filename)).encode("utf-8"))
            body.write(data)
            body.write(b"\r\n")
        body.write(("--%s--\r\n" % boundary).encode("utf-8"))
        req = urllib.request.Request(BASE + path, data=body.getvalue(), method="POST")
        req.add_header("Content-Type", "multipart/form-data; boundary=" + boundary)
        try:
            with client.opener.open(req) as r:
                payload = r.read()
                return r.status, (json.loads(payload.decode("utf-8")) if payload else None)
        except urllib.error.HTTPError as e:
            payload = e.read()
            try:
                return e.code, (json.loads(payload.decode("utf-8")) if payload else None)
            except Exception:
                return e.code, None

    def download(client, path):
        """โหลดไฟล์ที่ไม่ใช่ JSON · คืน (status, content-type, bytes)"""
        try:
            with client.opener.open(urllib.request.Request(BASE + path)) as r:
                return r.status, r.headers.get("Content-Type"), r.read()
        except urllib.error.HTTPError as e:
            return e.code, e.headers.get("Content-Type"), e.read()

    def errors_of(res):
        return [(e["row"], e["column"], e["message"]) for e in res["errors"]] if isinstance(res, dict) else res

    def field(res, key):
        return res.get(key) if isinstance(res, dict) else res

    status, room3 = owner("POST", "/api/terms/%d/classrooms" % term_id, {"name": "E2E นำเข้า"})
    record(g, "สร้างห้องสำหรับทดสอบนำเข้า", status)
    room3_id = room3["id"]
    base = "/api/classrooms/%d/import" % room3_id
    names = {s["studentCode"]: (s["firstName"], s["lastName"])
             for s in owner("GET", "/api/classrooms/%d/students" % room_id)[1]}
    a_first, a_last = names["90001"]
    b_first, b_last = names["90002"]
    roster_head = ["เลขที่", "รหัสนักเรียน", "ชื่อ", "นามสกุล"]
    roster_ok = xlsx([roster_head, [1, "90001", a_first, a_last], [2, "90002", b_first, b_last]])

    record(g, "คนไม่ล็อกอินโหลด template", download(anon, base + "/students/template")[0])
    record(g, "นักเรียนโหลด template", download(stu, base + "/students/template")[0])
    record(g, "ครูอื่นโหลด template ห้องที่ไม่ใช่ของตัวเอง", download(other, base + "/students/template")[0])
    for label, kind in (("นักเรียน", "students"), ("คะแนน", "scores")):
        status, ctype, data = download(owner, "%s/%s/template" % (base, kind))
        record(g, "โหลด template " + label, status)
        value(g, "  เป็นไฟล์ xlsx", (ctype, data[:2] == b"PK"))

    record(g, "ไม่ได้ส่งเป็นไฟล์ (JSON)", *owner("POST", base + "/students/preview", {"file": "x"}))
    record(g, "ส่ง form แต่ไม่มีไฟล์", *upload(owner, base + "/students/preview", [], [("note", "x")]))
    record(g, "ไฟล์ .csv", *upload(owner, base + "/students/preview", [("file", "roster.csv", b"a,b\n")]))
    record(g, "ไฟล์ขยะที่ตั้งชื่อเป็น .xlsx", *upload(owner, base + "/students/preview", [("file", "roster.xlsx", b"not excel")]))
    record(g, "ไฟล์ว่าง", *upload(owner, base + "/students/preview", [("file", "roster.xlsx", b"")]))
    record(g, "ไฟล์ใหญ่เกิน 2 MB", *upload(owner, base + "/students/preview",
                                            [("file", "roster.xlsx", b"0" * (2 * 1024 * 1024 + 1))]))
    record(g, "ส่ง 2 ไฟล์พร้อมกัน", *upload(owner, base + "/students/preview",
                                            [("file", "a.xlsx", roster_ok), ("file", "b.xlsx", roster_ok)]))
    record(g, "ครูอื่นตรวจไฟล์ห้องที่ไม่ใช่ของตัวเอง", *upload(other, base + "/students/preview", [("file", "r.xlsx", roster_ok)]))
    record(g, "นักเรียนตรวจไฟล์", *upload(stu, base + "/students/preview", [("file", "r.xlsx", roster_ok)]))
    record(g, "นักเรียนยืนยันนำเข้า", *upload(stu, base + "/scores/commit", [("file", "r.xlsx", roster_ok)]))

    roster_bad = xlsx([roster_head, [1, "90001", "ชื่อผิด", a_last], [1, "90002", b_first, b_last], [3, "9000#", "ก", "ข"]])
    status, res = upload(owner, base + "/students/preview", [("file", "roster.xlsx", roster_bad)])
    record(g, "ตรวจไฟล์รายชื่อที่มีจุดผิด", status)
    value(g, "  จุดผิดที่เจอ", errors_of(res))
    record(g, "ยืนยันไฟล์รายชื่อที่มีจุดผิด", *upload(owner, base + "/students/commit", [("file", "roster.xlsx", roster_bad)]))
    value(g, "  ห้องยังว่าง ไม่บันทึกอะไรเลย", owner("GET", "/api/classrooms/%d/students" % room3_id)[1])

    status, res = upload(owner, base + "/students/preview", [("file", "roster.xlsx", roster_ok)])
    record(g, "ตรวจไฟล์รายชื่อที่ถูกต้อง", status)
    value(g, "  สรุป", field(res, "summary"))
    value(g, "  ตรวจอย่างเดียวยังไม่บันทึก", owner("GET", "/api/classrooms/%d/students" % room3_id)[1])
    status, res = upload(owner, base + "/students/commit", [("file", "roster.xlsx", roster_ok)])
    record(g, "ยืนยันนำเข้ารายชื่อ", status)
    value(g, "  นักเรียนในห้องหลังนำเข้า",
          [(s["no"], s["studentCode"]) for s in owner("GET", "/api/classrooms/%d/students" % room3_id)[1]])
    status, res = upload(owner, base + "/students/preview", [("file", "roster.xlsx", roster_ok)])
    value(g, "  ตรวจไฟล์เดิมซ้ำ ไม่มีอะไรเปลี่ยน", (status, field(res, "hasChanges")))

    score_head = roster_head + ["งานกลุ่ม (10)"]
    scores_bad = xlsx([score_head, [1, "90001", a_first, a_last, 8.5], [2, "90002", b_first, b_last, 11],
                       [3, "90003", "ซี", "สาม", 5]])
    status, res = upload(owner, base + "/scores/preview", [("file", "scores.xlsx", scores_bad)])
    record(g, "ตรวจไฟล์คะแนนที่มีจุดผิด", status)
    value(g, "  จุดผิดที่เจอ", errors_of(res))
    record(g, "ยืนยันไฟล์คะแนนที่มีจุดผิด", *upload(owner, base + "/scores/commit", [("file", "scores.xlsx", scores_bad)]))
    value(g, "  ไม่มีรายการใหม่ถูกสร้าง", owner("GET", "/api/classrooms/%d/items" % room3_id)[1])
    status, res = upload(owner, base + "/students/preview", [("file", "s.xlsx", scores_bad)])
    record(g, "เอาไฟล์คะแนนไปใส่หน้านำเข้ารายชื่อ", status)
    value(g, "  จุดผิดที่เจอ", errors_of(res))

    scores_ok = xlsx([score_head, [1, "90001", a_first, a_last, 8.5], [2, "90002", b_first, b_last, None]])
    status, res = upload(owner, base + "/scores/preview", [("file", "scores.xlsx", scores_ok)])
    record(g, "ตรวจไฟล์คะแนนที่ถูกต้อง", status)
    value(g, "  สรุป", field(res, "summary"))
    record(g, "ยืนยันนำเข้าคะแนน", upload(owner, base + "/scores/commit", [("file", "scores.xlsx", scores_ok)])[0])
    status, grid = owner("GET", "/api/classrooms/%d/scores" % room3_id)
    value(g, "  รายการในห้อง", [(i["name"], i["maxScore"]) for i in grid["items"]])
    value(g, "  คะแนนที่บันทึก", [s["value"] for s in grid["scores"]])
    item3 = grid["items"][0]["id"] if grid["items"] else 0
    sid_a = next((s["studentId"] for s in grid["students"] if s["studentCode"] == "90001"), 0)
    value(g, "  มีประวัติการแก้ (audit)", [(a["oldValue"], a["newValue"]) for a in
                                          owner("GET", "/api/scores/audits?itemId=%d&studentId=%d" % (item3, sid_a))[1] or []])
    blank = xlsx([score_head, [1, "90001", a_first, a_last, None]])
    status, res = upload(owner, base + "/scores/preview", [("file", "scores.xlsx", blank)])
    value(g, "  ช่องว่างไม่ล้างคะแนนเดิม", (status, field(res, "hasChanges")))

    # เก็บกวาดห้องนำเข้า: ล้างคะแนน → ลบรายการ → เอานักเรียนออก → ลบห้อง
    owner("PUT", "/api/scores", {"itemId": item3, "studentId": sid_a, "value": None, "expected": 8.5})
    owner("DELETE", "/api/items/%d" % item3)
    for s in owner("GET", "/api/classrooms/%d/students" % room3_id)[1] or []:
        owner("DELETE", "/api/classrooms/%d/students/%d" % (room3_id, s["studentId"]))
    owner("DELETE", "/api/classrooms/%d" % room3_id)

    # ---------------------------------------------------------------- ดูคะแนนด่วน (ไม่ล็อกอิน)
    g = "PUBLIC"
    pub = Client()
    status, rooms = pub("GET", "/api/public/classrooms")
    record(g, "ดึงห้องสำหรับ dropdown", status)
    mine = next((r for r in rooms if r["id"] == room_id), None)
    value(g, "  ห้องที่เปิดอยู่ใน dropdown", mine is not None)
    value(g, "  เลขที่ที่มีจริง", mine["nos"] if mine else None)
    dumped = json.dumps(rooms, ensure_ascii=False)
    value(g, "  ไม่มีชื่อหรือรหัสนักเรียนหลุด", "เอใหม่" not in dumped and "90001" not in dumped and "firstName" not in dumped)
    status, res = pub("POST", "/api/public/scores", {"classroomId": room_id, "no": 20, "studentCode": "90001"})
    record(g, "กรอกครบถูกต้อง", status)
    value(g, "  ชื่อที่แสดง", res.get("name") if isinstance(res, dict) else None)
    value(g, "  คะแนนรวม", "%s / %s" % (res.get("total"), res.get("full")) if isinstance(res, dict) else None)
    record(g, "รหัสผิด", *pub("POST", "/api/public/scores", {"classroomId": room_id, "no": 20, "studentCode": "99999"}))
    record(g, "เลขที่ผิด", *pub("POST", "/api/public/scores", {"classroomId": room_id, "no": 2, "studentCode": "90001"}))
    record(g, "ห้องผิด", *pub("POST", "/api/public/scores", {"classroomId": room2_id, "no": 20, "studentCode": "90001"}))
    record(g, "รหัสมีอักขระแปลก", *pub("POST", "/api/public/scores", {"classroomId": room_id, "no": 20, "studentCode": "9000#"}))
    value(g, "  ไม่สร้าง cookie", len(pub.jar))
    record(g, "คนไม่ล็อกอินสั่งปิด", *pub("PATCH", "/api/terms/%d/public-scores" % term_id, {"enabled": False}))
    record(g, "ครูอื่นสั่งปิดของภาคเรียนเรา", *other("PATCH", "/api/terms/%d/public-scores" % term_id, {"enabled": False}))
    record(g, "เจ้าของสั่งปิด", *owner("PATCH", "/api/terms/%d/public-scores" % term_id, {"enabled": False}))
    status, rooms = pub("GET", "/api/public/classrooms")
    value(g, "  ปิดแล้วห้องหายจาก dropdown", all(r["id"] != room_id for r in rooms))
    record(g, "ปิดแล้วกรอกถูกก็ดูไม่ได้", *pub("POST", "/api/public/scores",
                                              {"classroomId": room_id, "no": 20, "studentCode": "90001"}))
    record(g, "เจ้าของเปิดกลับ", *owner("PATCH", "/api/terms/%d/public-scores" % term_id, {"enabled": True}))

    # ---------------------------------------------------------------- เจ้าของ
    g = "OWNER"
    status, staff = owner("GET", "/api/staff")
    record(g, "ดูรายชื่อครู", status)
    me_row = next((s for s in staff if s.get("isMe")), {})
    record(g, "ลดยศตัวเอง", *owner("PATCH", "/api/staff/%d" % me_row.get("id", 0), {"role": "Teacher"}))
    record(g, "ลบตัวเอง", *owner("DELETE", "/api/staff/%d" % me_row.get("id", 0)))
    record(g, "เพิ่มครูอีเมลซ้ำ", *owner("POST", "/api/staff", {"email": OWNER, "role": "Teacher"}))
    record(g, "อีเมลผิดรูปแบบ", *owner("POST", "/api/staff", {"email": "ไม่ใช่อีเมล", "role": "Teacher"}))
    record(g, "อีเมลว่าง", *owner("POST", "/api/staff", {"email": "", "role": "Teacher"}))
    record(g, "ยศที่ไม่มีอยู่จริง", *owner("POST", "/api/staff", {"email": "q@q.com", "role": "Admin"}))
    record(g, "แก้ยศครูที่ไม่มีอยู่", *owner("PATCH", "/api/staff/999999", {"role": "Owner"}))

    # ---------------------------------------------------------------- เก็บกวาด
    # คืนชื่อเดิมก่อน เพราะแถว Student อยู่ข้ามภาคเรียน ถ้าไม่คืนรอบหน้าจะอ่านได้ชื่อที่แก้ไว้
    owner("PATCH", "/api/classrooms/%d/students/%d" % (room_id, sid1),
          {"firstName": "เอ", "lastName": "หนึ่ง", "no": 1})
    owner("PUT", "/api/scores", {"itemId": item_id, "studentId": sid1, "value": None, "expected": 18})
    owner("DELETE", "/api/items/%d" % item_id)
    for rid in (room_id, room2_id):
        for s in owner("GET", "/api/classrooms/%d/students" % rid)[1] or []:
            owner("DELETE", "/api/classrooms/%d/students/%d" % (rid, s["studentId"]))
        for i in owner("GET", "/api/classrooms/%d/items" % rid)[1] or []:
            owner("DELETE", "/api/items/%d" % i["id"])
        owner("DELETE", "/api/classrooms/%d" % rid)
    owner("DELETE", "/api/terms/%d" % term_id)
    for s in owner("GET", "/api/staff")[1] or []:
        if s["email"] == "e2e.teacher@example.com":
            owner("DELETE", "/api/staff/%d" % s["id"])
    return True


def rate_limit_check():
    """ไม่อยู่ใน snapshot เพราะผลขึ้นกับว่ายิงไปกี่ครั้งในนาทีนั้น"""
    anon = Client()
    codes = [anon("POST", "/api/auth/claim", {"studentCode": "99999"})[0] for _ in range(14)]
    limited = codes.count(429)
    print("\nrate limit: ยิง 14 ครั้ง โดนจำกัด %d ครั้ง (เริ่มที่ครั้งที่ %s)"
          % (limited, codes.index(429) + 1 if 429 in codes else "-"))
    print("ผล:", "PASS" if limited > 0 else "FAIL — rate limit ไม่ทำงาน")


def main():
    if not wait_for_api():
        print("ต่อ API ที่ %s ไม่ได้ — สั่ง dotnet run --project backend/src/Api ก่อน" % BASE)
        return 1

    if not run():
        return 1

    actual = "\n".join(lines) + "\n"

    if "--update" in sys.argv:
        io.open(SNAPSHOT, "w", encoding="utf-8", newline="\n").write(actual)
        print("บันทึก snapshot %d บรรทัด -> %s" % (len(lines), SNAPSHOT))
        if "--ratelimit" in sys.argv:
            rate_limit_check()
        return 0

    if not os.path.exists(SNAPSHOT):
        print("ยังไม่มี snapshot — สั่งด้วย --update ก่อน")
        return 1

    expected = io.open(SNAPSHOT, encoding="utf-8").read()
    if actual == expected:
        print("snapshot ตรงกันทั้ง %d เคส" % len(lines))
        if "--ratelimit" in sys.argv:
            rate_limit_check()
        return 0

    exp_lines = expected.strip().split("\n")
    act_lines = actual.strip().split("\n")
    print("snapshot ไม่ตรงกัน")
    print("-" * 78)
    for i in range(max(len(exp_lines), len(act_lines))):
        e = exp_lines[i] if i < len(exp_lines) else "(ไม่มีบรรทัดนี้)"
        a = act_lines[i] if i < len(act_lines) else "(ไม่มีบรรทัดนี้)"
        if e != a:
            print("บรรทัด %d" % (i + 1))
            print("  ที่คาดไว้ : %s" % e)
            print("  ที่ได้จริง: %s" % a)
    print("-" * 78)
    print("ถ้าเปลี่ยนโดยตั้งใจ ให้รันซ้ำด้วย --update แล้ว commit snapshot ใหม่")
    return 1


if __name__ == "__main__":
    sys.exit(main())
