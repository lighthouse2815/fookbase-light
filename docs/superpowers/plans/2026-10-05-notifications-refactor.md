# Notifications Implementation Plan

> **For agentic workers:** Use the existing Identity/Pages conventions and complete each task with its own checks, Vietnamese commit and push.

**Goal:** Tổ chức Notifications giống Identity và Pages, giữ hợp đồng API hiện tại.

**Architecture:** Dùng MVC controller, mỗi DTO/enum một file và entity trong `Domain/Entities`. Thay EF configuration bằng annotation và khai báo quan hệ rõ ràng; service, SignalR hub và push worker giữ nguyên trách nhiệm.

**Tech Stack:** ASP.NET Core 10, EF Core 10, Npgsql, xUnit.

**Spec:** Yêu cầu trong phiên làm việc: áp dụng cách cấu trúc Identity/Pages cho Notifications.

## Global Constraints

- Giữ 6 route, HTTP method, authorization, JSON field, mã lỗi và cursor mặc định.
- Giữ giá trị số của enum và namespace entity như Identity/Pages.
- Không thêm dependency hoặc đưa các thay đổi Games/Users/frontend vào commit.
- Không áp dụng migration lên database của người dùng.

## Review Focus

- Token không hợp lệ khi hủy đăng ký vẫn trả 204 và không thay đổi thiết bị.
- Lỗi cursor và lỗi không tìm thấy notification vẫn có JSON code hiện tại.
- Body DELETE được bind đúng và chỉ chủ thiết bị được hủy đăng ký.
- ActorUserId nullable và EntityId đa loại không tạo quan hệ sai.
- Annotation giữ index, độ dài cột, kiểu enum và cascade receipt hiện tại.

### Task 1: Controller, DTO và domain

**Files:** Notifications/Controllers/NotificationsController.cs, DTOs/Requests/PushTokenRequest.cs, DTOs/Responses/{NotificationResponse,NotificationPageResponse,NotificationCountResponse}.cs, Domain/{Entities,Enums}; Program.cs; NotificationControllerContractTests.cs và các import enum hiện tại.

- [x] Thêm và chạy test contract: 6 API được đăng ký qua MVC, yêu cầu JWT.
- [x] Chuyển endpoint sang controller, tách DTO/enum, di chuyển entity, cập nhật import và đăng ký Program.
- [x] Build; chạy test notification/cursor, push registration và enum hiện có cùng test contract mới.
- [x] Review diff; commit `refactor: tổ chức module Notifications theo Identity`; push origin/main.

### Task 2: Annotation và quan hệ

**Files:** Notifications/Domain/Entities/*.cs, Notifications/Data/Configurations/*.cs, NotificationModelTests.cs, các fixture Friends/Posts/Messages, migration AddNotificationRelationships và snapshot.

- [x] Thêm và chạy test model yêu cầu FK recipient/actor/device user, navigation receipt/device, không có shadow property.
- [x] Chuyển table/key/index/độ dài sang annotation; user FK Restrict, receipt FK Cascade như trước.
- [x] Tạo và review migration: chỉ FK/index thuộc Notifications; không đổi cột dữ liệu.
- [x] Build; chạy model và các nhóm integration bị ảnh hưởng trên PostgreSQL tạm.
- [x] Review diff; commit `refactor: dùng annotation và ánh xạ quan hệ Notifications`; push origin/main.

## Verification record

- Task 1: `cd402f6`, push origin/main thành công; 20 test liên quan pass, API build 0 warning/error.
- Task 2: 4 trường hợp quan hệ fail trước khi triển khai; 6 model test pass sau triển khai.
- Review độc lập cả hai phần: không có finding cần sửa.
- Lỗi query sai kiểu dùng response validation MVC hiện có; HTTP 400 và mã lỗi cursor nghiệp vụ được giữ.
- Fixture push token, truy cập quan hệ và 9 test lời mời kết bạn tạo User thật bằng helper hiện có, thay vì GUID chưa được lưu; giữ các assertion.
- Kiểm tra cuối: build solution 0 warning/error; 587/587 test pass ở 9 nhóm (TZ=UTC, database PostgreSQL tạm riêng từng nhóm). Friends/Posts được chạy lại sau khi sửa fixture.
- Migration chỉ được áp dụng vào database test; container test đã được dọn.
