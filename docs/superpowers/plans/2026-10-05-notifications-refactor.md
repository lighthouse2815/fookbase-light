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
- [ ] Review diff; commit `refactor: tổ chức module Notifications theo Identity`; push origin/main.

### Task 2: Annotation và quan hệ

**Files:** Notifications/Domain/Entities/*.cs, Notifications/Data/Configurations/*.cs, NotificationModelTests.cs, migration AddNotificationRelationships và snapshot.

- [ ] Thêm và chạy test model yêu cầu FK recipient/actor/device user, navigation receipt/device, không có shadow property.
- [ ] Chuyển table/key/index/độ dài sang annotation; user FK Restrict, receipt FK Cascade như trước.
- [ ] Tạo và review migration: chỉ FK/index thuộc Notifications; không đổi cột dữ liệu.
- [ ] Build; chạy model và các nhóm integration bị ảnh hưởng trên PostgreSQL tạm.
- [ ] Review diff; commit `refactor: dùng annotation và ánh xạ quan hệ Notifications`; push origin/main.
