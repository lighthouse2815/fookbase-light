-- Chỉ dùng sau khi backup và xác nhận schema đang ở mốc 20261006182552.
-- Giữ toàn bộ dữ liệu, schema và 53 bản ghi migration cũ.
-- Không chạy script này trên database trống: dùng dotnet ef database update.
BEGIN;

LOCK TABLE public."__EFMigrationsHistory" IN ACCESS EXCLUSIVE MODE;

DO $baseline$
DECLARE
    baseline_id constant text := '20261010061733_InitialFookbaseBaseline';
    legacy_ids constant text[] := ARRAY[
        '20260910143327_InitialFookbase',
        '20260910154744_AddNotifications',
        '20260910162758_AddFeedPostIndex',
        '20260910165428_AddGroups',
        '20260911100000_AddMessengerConversations',
        '20260911180905_AddReelsVideoProcessing',
        '20260911185716_AddStoriesV1',
        '20260911195628_AddPagesV1',
        '20260912093027_AddSocialInteractionsV1',
        '20260912100000_AddGlobalSearchIndexes',
        '20260913174938_AddEventsV1',
        '20260913195007_AddPhotosAlbumsV1',
        '20260913203000_AddUserFollowV1',
        '20260913211035_AddMemoriesBirthdaysProfileExtrasV1',
        '20260913215835_AddPrivacySettingsV1',
        '20260913220334_AddAuthSessionsV1',
        '20260913231340_AddTwoFactorLoginChallengesV1',
        '20260914063800_AddModerationV1',
        '20260914074422_AddFeedRankingV2Indexes',
        '20260914081311_AddMediaDeletionRetryMetadata',
        '20260915215651_AddGoogleAuthentication',
        '20260916030802_AddContactOtpRegistration',
        '20260916031719_AddUserProfileGender',
        '20260916031831_AddRegistrationChallengeGender',
        '20260916033022_PersistUserProfileGender',
        '20260916034057_AddPhonePasswordResetOtp',
        '20260916035017_AddUniquePhoneNumber',
        '20260916065000_AddPinnedProfilePosts',
        '20260920172052_AddPostTextBackground',
        '20260923191545_AddZolaPushNotifications',
        '20260924042420_RemoveUserIsActiveDefault',
        '20260925002745_AddAuthSessionUserForeignKey',
        '20260925013216_RequireRefreshTokenSession',
        '20260925024527_UseRefreshTokenAnnotations',
        '20260928125228_RenameExternalLoginCompletionToTicket',
        '20260928133705_RequireExternalLoginTicketUser',
        '20261001094831_RenamePasswordResetOtpTable',
        '20261001102732_RenamePasswordResetOtpContactToPhoneNumber',
        '20261001105826_RenameOtpResendRateLimitProperties',
        '20261002083820_AddTwoFactorLoginChallengeUserForeignKey',
        '20261004094723_AddUserProfileRelationships',
        '20261004163955_AddStoryRelationships',
        '20261004171214_AddPageRelationships',
        '20261004173030_AddNotificationRelationships',
        '20261004214655_AddMessageRelationships',
        '20261004221902_AddMediaRelationships',
        '20261004224859_AddGroupRelationships',
        '20261005061005_AddAdminRelationships',
        '20261005061748_AddEventRelationships',
        '20261005061804_AddFriendRelationships',
        '20261006165542_AddPhotoRelationships',
        '20261006175100_AddPostRelationships',
        '20261006182552_AddReelViewRelationships'
    ];
    applied_ids text[];
BEGIN
    IF EXISTS (
        SELECT 1 FROM public."__EFMigrationsHistory"
        WHERE "MigrationId" = baseline_id
    ) THEN
        RAISE NOTICE 'Baseline already recorded; no changes made.';
        RETURN;
    END IF;

    SELECT array_agg("MigrationId"::text ORDER BY "MigrationId" COLLATE "C")
    INTO applied_ids
    FROM public."__EFMigrationsHistory";

    IF applied_ids IS DISTINCT FROM legacy_ids THEN
        RAISE EXCEPTION 'Legacy migration history does not match the 53 expected migrations. Update with the original migrations and verify schema before retrying.';
    END IF;

    INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES (baseline_id, '10.0.11');
END
$baseline$;

COMMIT;
