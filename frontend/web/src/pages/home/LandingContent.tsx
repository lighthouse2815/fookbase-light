import * as React from 'react'

export default function LandingContent(): React.ReactElement {
  return (
    <div lang="vi">
      <main className="min-h-screen bg-bg text-text">
      <header className="border-b border-border bg-surface/95 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-4 sm:px-6 lg:px-8">
          <a href="/" className="flex items-center gap-2.5 text-text no-underline" aria-label="Fookbase">
            <span className="grid h-10 w-10 place-items-center rounded-xl bg-primary text-xl font-extrabold text-white">f</span>
            <span className="font-heading text-xl font-extrabold tracking-tight">Fookbase</span>
          </a>
          <nav className="hidden items-center gap-5 text-sm font-semibold text-text-muted sm:flex" aria-label="Điều hướng trang chủ">
            <a href="#tinh-nang" className="transition hover:text-primary">Tính năng</a>
            <a href="#quyen-rieng-tu" className="transition hover:text-primary">Quyền riêng tư</a>
            <a href="#cau-hoi" className="transition hover:text-primary">Câu hỏi</a>
          </nav>
          <a href="/login" className="rounded-lg bg-primary px-4 py-2 text-sm font-bold text-white no-underline transition hover:bg-primary-dark">
            Đăng nhập
          </a>
        </div>
      </header>

      <section className="relative overflow-hidden border-b border-border">
        <div className="absolute inset-x-0 top-0 -z-0 h-72 bg-[radial-gradient(circle_at_top_left,rgba(24,119,242,0.2),transparent_55%)]" aria-hidden="true" />
        <div className="relative z-10 mx-auto grid max-w-6xl gap-12 px-4 py-16 sm:px-6 sm:py-20 lg:grid-cols-[minmax(0,1.1fr)_minmax(21rem,0.9fr)] lg:items-center lg:px-8 lg:py-28">
          <div>
            <p className="mb-4 inline-flex rounded-full border border-primary/30 bg-primary/10 px-3 py-1 text-xs font-bold uppercase tracking-[0.16em] text-primary">
              Fookbase Light · Không gian xã hội của bạn
            </p>
            <h1 className="max-w-3xl font-heading text-4xl font-extrabold leading-tight tracking-tight text-text sm:text-5xl lg:text-6xl">
              Fookbase – Mạng xã hội để kết nối, chia sẻ và trò chuyện.
            </h1>
            <p className="mt-6 max-w-2xl text-base leading-7 text-text-muted sm:text-lg">
              Fookbase, còn được biết đến với tên Fookbase Light (fookbase-light), giúp bạn giữ liên lạc với những người quan trọng, chia sẻ những điều có ý nghĩa và khám phá cộng đồng theo cách gần gũi hơn.
            </p>
            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <a href="/login" className="rounded-xl bg-primary px-5 py-3 text-center text-sm font-bold text-white no-underline shadow-lg shadow-primary/20 transition hover:bg-primary-dark">
                Tham gia Fookbase
              </a>
              <a href="#tinh-nang" className="rounded-xl border border-border bg-surface px-5 py-3 text-center text-sm font-bold text-text no-underline transition hover:border-primary/60 hover:bg-surface-2">
                Khám phá tính năng
              </a>
            </div>
          </div>

          <aside className="rounded-3xl border border-border bg-surface p-4 shadow-xl shadow-black/10 sm:p-5" aria-label="Những điều bạn có thể làm trên Fookbase">
            <div className="rounded-2xl bg-surface-2 p-4 sm:p-5">
              <div className="flex items-center gap-3">
                <span className="grid h-11 w-11 place-items-center rounded-full bg-primary text-lg font-extrabold text-white" aria-hidden="true">f</span>
                <div>
                  <p className="font-heading text-base font-bold text-text">Cùng nhau, gần hơn</p>
                  <p className="text-sm text-text-muted">Chia sẻ, theo dõi và trò chuyện.</p>
                </div>
              </div>
              <div className="mt-5 grid gap-3">
                <div className="rounded-xl border border-border bg-surface p-3">
                  <p className="text-sm font-bold text-text">Bài viết, ảnh và video</p>
                  <p className="mt-1 text-sm leading-5 text-text-muted">Lưu lại khoảnh khắc và bắt đầu cuộc trò chuyện với bạn bè.</p>
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div className="rounded-xl bg-primary/10 p-3">
                    <p className="text-sm font-bold text-primary">Nhóm &amp; Trang</p>
                    <p className="mt-1 text-xs leading-5 text-text-muted">Theo dõi cộng đồng bạn quan tâm.</p>
                  </div>
                  <div className="rounded-xl bg-surface p-3 ring-1 ring-border">
                    <p className="text-sm font-bold text-text">Zola Light</p>
                    <p className="mt-1 text-xs leading-5 text-text-muted">Trò chuyện riêng tư với bạn bè.</p>
                  </div>
                </div>
              </div>
            </div>
          </aside>
        </div>
      </section>

      <section id="tinh-nang" className="scroll-mt-6 mx-auto max-w-6xl px-4 py-16 sm:px-6 sm:py-20 lg:px-8">
        <div className="max-w-2xl">
          <p className="text-sm font-bold uppercase tracking-[0.16em] text-primary">Tính năng</p>
          <h2 className="mt-3 font-heading text-3xl font-extrabold tracking-tight text-text sm:text-4xl">Mọi điều cần thiết để giữ kết nối.</h2>
          <p className="mt-4 leading-7 text-text-muted">Từ những cập nhật hằng ngày đến cộng đồng và cuộc trò chuyện riêng, Fookbase tập trung vào những người và nội dung bạn quan tâm.</p>
        </div>
        <div className="mt-10 grid gap-4 md:grid-cols-2">
          <article className="rounded-2xl border border-border bg-surface p-6">
            <p className="text-2xl" aria-hidden="true">✦</p>
            <h3 className="mt-4 font-heading text-xl font-bold text-text">Chia sẻ theo cách của bạn</h3>
            <p className="mt-2 leading-6 text-text-muted">Đăng bài viết, ảnh và video; chia sẻ Stories hoặc tạo Reels để kể những khoảnh khắc theo cách phù hợp với bạn.</p>
          </article>
          <article className="rounded-2xl border border-border bg-surface p-6">
            <p className="text-2xl" aria-hidden="true">◎</p>
            <h3 className="mt-4 font-heading text-xl font-bold text-text">Gần gũi với vòng kết nối của bạn</h3>
            <p className="mt-2 leading-6 text-text-muted">Kết bạn, theo dõi và xem bảng tin xoay quanh bạn bè, Nhóm và Trang bạn đã chọn.</p>
          </article>
          <article className="rounded-2xl border border-border bg-surface p-6">
            <p className="text-2xl" aria-hidden="true">◌</p>
            <h3 className="mt-4 font-heading text-xl font-bold text-text">Khám phá cộng đồng và sự kiện</h3>
            <p className="mt-2 leading-6 text-text-muted">Tham gia Nhóm, theo dõi Trang, tạo hoặc khám phá sự kiện, lưu ảnh vào Album và nhìn lại Kỷ niệm.</p>
          </article>
          <article className="rounded-2xl border border-border bg-surface p-6">
            <p className="text-2xl" aria-hidden="true">↗</p>
            <h3 className="mt-4 font-heading text-xl font-bold text-text">Trò chuyện với Zola Light</h3>
            <p className="mt-2 leading-6 text-text-muted">Gửi tin nhắn riêng tư, trao đổi trong cuộc trò chuyện nhóm và tiếp tục câu chuyện với bạn bè trên Zola Light.</p>
          </article>
        </div>
      </section>

      <section id="quyen-rieng-tu" className="scroll-mt-6 border-y border-border bg-surface-2">
        <div className="mx-auto grid max-w-6xl gap-8 px-4 py-16 sm:px-6 sm:py-20 lg:grid-cols-[minmax(0,0.85fr)_minmax(0,1.15fr)] lg:items-start lg:px-8">
          <div>
            <p className="text-sm font-bold uppercase tracking-[0.16em] text-primary">Quyền riêng tư</p>
            <h2 className="mt-3 font-heading text-3xl font-extrabold tracking-tight text-text sm:text-4xl">Bạn quyết định cách mình kết nối.</h2>
            <p className="mt-4 leading-7 text-text-muted">Fookbase có các thiết lập để bạn quản lý đối tượng xem bài viết, lời mời kết bạn và khả năng hiển thị danh sách kết nối.</p>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <article className="rounded-2xl border border-border bg-surface p-5">
              <h3 className="font-heading text-lg font-bold text-text">Thiết lập quyền riêng tư</h3>
              <p className="mt-2 text-sm leading-6 text-text-muted">Chọn đối tượng mặc định cho bài viết, ai có thể gửi lời mời kết bạn và ai có thể xem danh sách bạn bè hoặc đang theo dõi của bạn.</p>
            </article>
            <article className="rounded-2xl border border-border bg-surface p-5">
              <h3 className="font-heading text-lg font-bold text-text">Xác thực hai bước</h3>
              <p className="mt-2 text-sm leading-6 text-text-muted">Bạn có thể bật xác thực hai bước trong phần bảo mật tài khoản để bổ sung một bước xác minh khi đăng nhập.</p>
            </article>
          </div>
        </div>
      </section>

      <section id="cau-hoi" className="scroll-mt-6 mx-auto max-w-4xl px-4 py-16 sm:px-6 sm:py-20 lg:px-8">
        <div className="text-center">
          <p className="text-sm font-bold uppercase tracking-[0.16em] text-primary">Câu hỏi thường gặp</p>
          <h2 className="mt-3 font-heading text-3xl font-extrabold tracking-tight text-text sm:text-4xl">Tìm hiểu nhanh về Fookbase</h2>
        </div>
        <div className="mt-10 divide-y divide-border rounded-2xl border border-border bg-surface px-5 sm:px-6">
          <details className="group py-5">
            <summary className="cursor-pointer list-none pr-8 font-heading text-lg font-bold text-text marker:content-none">Fookbase là gì?<span className="float-right text-primary group-open:rotate-45" aria-hidden="true">+</span></summary>
            <p className="mt-3 max-w-3xl leading-7 text-text-muted">Fookbase là không gian mạng xã hội để kết nối với bạn bè, chia sẻ nội dung và tham gia những cộng đồng bạn quan tâm.</p>
          </details>
          <details className="group py-5">
            <summary className="cursor-pointer list-none pr-8 font-heading text-lg font-bold text-text marker:content-none">Tôi có thể chia sẻ những nội dung nào?<span className="float-right text-primary group-open:rotate-45" aria-hidden="true">+</span></summary>
            <p className="mt-3 max-w-3xl leading-7 text-text-muted">Bạn có thể tạo bài viết, chia sẻ ảnh và video, đăng Stories, tạo Reels và tương tác với nội dung từ bạn bè hoặc cộng đồng.</p>
          </details>
          <details className="group py-5">
            <summary className="cursor-pointer list-none pr-8 font-heading text-lg font-bold text-text marker:content-none">Zola Light dùng để làm gì?<span className="float-right text-primary group-open:rotate-45" aria-hidden="true">+</span></summary>
            <p className="mt-3 max-w-3xl leading-7 text-text-muted">Zola Light là không gian nhắn tin của Fookbase, nơi bạn có thể trao đổi riêng với bạn bè hoặc trò chuyện cùng một nhóm.</p>
          </details>
          <details className="group py-5">
            <summary className="cursor-pointer list-none pr-8 font-heading text-lg font-bold text-text marker:content-none">Tôi có thể điều chỉnh quyền riêng tư không?<span className="float-right text-primary group-open:rotate-45" aria-hidden="true">+</span></summary>
            <p className="mt-3 max-w-3xl leading-7 text-text-muted">Có. Phần cài đặt cho phép bạn điều chỉnh quyền riêng tư của bài viết và mối quan hệ; phần bảo mật có tùy chọn xác thực hai bước.</p>
          </details>
        </div>
        <div className="mt-10 rounded-2xl bg-primary px-6 py-8 text-center text-white sm:px-10">
          <h2 className="font-heading text-2xl font-extrabold">Sẵn sàng kết nối theo cách của bạn?</h2>
          <p className="mx-auto mt-3 max-w-xl text-sm leading-6 text-white/85">Đăng nhập để tiếp tục với Fookbase hoặc bắt đầu tạo tài khoản từ cùng một nơi.</p>
          <a href="/login" className="mt-6 inline-flex rounded-xl bg-white px-5 py-3 text-sm font-bold text-primary no-underline transition hover:bg-surface-2">Đăng nhập hoặc tham gia Fookbase</a>
        </div>
      </section>
      </main>
    </div>
  )
}
