# System Screen Layout Index & Role Access Documentation (dillustration Platform)

This document provides a comprehensive mapping of all System Screens (SCR-01 through SCR-26) and Figures across all system features, categorized strictly by **Role Access Scope** and aligned with `d:\SEP490_Final\Web\dil-backend\docs\main.md`.

---

## 📊 Complete Screen Index Table (Categorized by Role Visibility)

### 🌐 1. Public / Guest Screens (Accessible to All Users)
| Screen ID | Interface Name | Route / Component | Description & Primary Features | Preview HTML Link |
| :--- | :--- | :--- | :--- | :--- |
| **SCR-01** | Authentication Suite | `/login`, `/register`, `/verify-otp`, `/forgot-password` | Login, Register, 6-digit OTP verification, Password recovery. | • [scr01_login_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr01_login_preview.html)<br>• [scr01_register_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr01_register_preview.html)<br>• [scr01_forgot_password_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr01_forgot_password_preview.html)<br>• [scr01_verify_otp_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr01_verify_otp_preview.html) |
| **SCR-05** | Marketplace Feed & Discovery | `/` | Infinite scroll gallery, AI recommendations, category tabs. | [scr05_marketplace_feed_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr05_marketplace_feed_preview.html) |
| **SCR-06** | Search & Advanced Filter | `/search` | Price range slider ($0-$2,000+), style checkboxes, cloud tags, **"Hide AI-Generated Artworks (`Tag.IsAiGenerated`)"** toggle, sort dropdown, bento grid. | [scr06_artwork_search_filter_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr06_artwork_search_filter_preview.html) |
| **SCR-07** | Artwork Details & Creator CTA | `/artwork/[id]` | Watermarked full preview, creator mini profile, tags, CTA. | [scr07_artwork_details_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr07_artwork_details_preview.html) |
| **SCR-08** | Creator Profile & Rate Cards | `/creator/[id]` | Public portfolio grid (6 samples + speedpaint video proof), rate card tiers, rating avg, slots counter. | [scr08_creator_profile_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr08_creator_profile_preview.html) |

---

### 🔵 2. Client-Only Screens (Buyer Role)
| Screen ID | Interface Name | Route / Component | Description & Primary Features | Preview HTML Link |
| :--- | :--- | :--- | :--- | :--- |
| **SCR-25** | Client Dashboard Overview | `/client/dashboard` | Client metrics (Active Commissions, Spent, Following, Approvals), active orders widget with mini stepper, Dual-Role Switcher (`[Switch to Creator Mode]`). | [scr25_client_dashboard_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr25_client_dashboard_preview.html) |
| **SCR-10** | Commission Request Wizard | `/commission/create` | Step 1 Scope brief & references, Step 2 timeline & 4-milestone calculation (Figure 34). | • [scr10_commission_request_brief_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr10_commission_request_brief_preview.html)<br>• [scr10_milestone_deadline_picker_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr10_milestone_deadline_picker_preview.html) |
| **SCR-11** | Escrow Payment Checkout | `/payment/checkout/[id]` | Order summary, payment gateway selectors (VNPay, MoMo, Wallet), platform escrow guarantee, HMAC-SHA256 IPN webhook simulator. | [scr11_escrow_checkout_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr11_escrow_checkout_preview.html) |
| **SCR-13 (Client)** | Client Commission Workroom | `/workroom/[id]` | 4-milestone stepper, WIP watermarked viewer, **Approve Milestone & Release Escrow**, **Request Revision**, **Raise Dispute**, SignalR 1-1 Chat. | [scr13_commission_workroom_client_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr13_commission_workroom_client_preview.html) |
| **SCR-15 Modal**| Creator Rating & Review | `/workroom/[id]/review` | 1–5 Star rating selector, feedback comments updating Creator RatingAvg. | [scr15_creator_rating_review_modal_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr15_creator_rating_review_modal_preview.html) |

---

### 🟡 3. Creator-Only Screens (Artist Role)
| Screen ID | Interface Name | Route / Component | Description & Primary Features | Preview HTML Link |
| :--- | :--- | :--- | :--- | :--- |
| **SCR-26** | Creator Studio & Analytics | `/creator/dashboard` | Business metrics (Monthly Revenue, Pending Requests, Rating Avg, Slots 3/5), Incoming Requests Widget, Dual-Role Switcher (`[Switch to Client Mode]`). | [scr26_creator_dashboard_analytics_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr26_creator_dashboard_analytics_preview.html) |
| **SCR-12** | Commission Request Review | `/creator/request-review/[id]` | Request review screen, slot availability check, Accept/Reject/Clarify actions. | [scr12_commission_request_review_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr12_commission_request_review_preview.html) |
| **SCR-13 (Creator)** | Creator Workroom Studio | `/creator/workroom/[id]` | 4-milestone stepper, **WIP Progress Drag-and-Drop Uploader**, **Final Deliverables Master Package Uploader** (.PSD, .SAI, .ZIP), Revision limit tracker `1/3`, SignalR 1-1 Chat. | [scr13_commission_workroom_creator_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr13_commission_workroom_creator_preview.html) |
| **SCR-19 Rate**| Rate Card & Services Config | `/creator/dashboard/rate-card` | Configure service package pricing, revision limits, and total commission slots (`CommissionSlots`). | [scr08_creator_profile_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr08_creator_profile_preview.html) |
| **SCR-21 Port**| Portfolio Inventory Studio | `/creator/dashboard/portfolio` | Drag & drop artwork uploader, AI Cloud Vision NSFW scan trigger, style tags selector, **"Declare AI-Generated Artwork (`Tag.IsAiGenerated`)"** toggle, inventory ledger table. | [scr21_creator_portfolio_inventory_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr21_creator_portfolio_inventory_preview.html) |

---

### 🤝 4. Shared Screens (Client & Creator Collaboration)
| Screen ID | Interface Name | Route / Component | Description & Primary Features | Preview HTML Link |
| :--- | :--- | :--- | :--- | :--- |
| **SCR-15** | Order Completion & Master Download | `/workroom/[id]/download` | 4K artwork preview, master package zip download via 15-min AWS S3 presigned URL. | [scr15_order_completed_download_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr15_order_completed_download_preview.html) |
| **SCR-16/17**| Wallet & Bank Payout | `/wallet` | 3 Metric Cards (Neon purple Available Balance, Escrow Held Amount, Total Withdrawn Revenue), Bank Payout Request form & modal, filterable Transaction Log Table (+/- amounts). | • [scr16_wallet_transaction_history_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr16_wallet_transaction_history_preview.html)<br>• [scr17_payout_request_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr17_payout_request_preview.html) |
| **SCR-19** | Raise Dispute Request Form | `/workroom/[id]/dispute` | Dispute category selector, issue description, evidence uploader. | [scr19_raise_dispute_request_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/scr19_raise_dispute_request_preview.html) |
| **SCR-04** | Account Settings & Security | `/profile/settings` | Profile edit, password change, active session devices, 2FA. | N/A |
| **Global** | Global Notification Center | Drawer (System-wide) | In-app real-time activity notifications drawer. | [global_notification_center_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/global_notification_center_preview.html) |

---

### 🔴 5. Admin / Moderator Only Screens (Platform Governance - Folder: `docs/admin/`)
| Screen ID | Interface Name | Route / Component | Description & Primary Features | Preview HTML Link |
| :--- | :--- | :--- | :--- | :--- |
| **SCR-24** | Admin System Overview & KPIs | `/admin` | Total platform GMV, active escrow locked, net platform revenue (10%), user growth metrics, Admin Sidebar. | [admin/scr24_admin_dashboard_kpi_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/admin/scr24_admin_dashboard_kpi_preview.html) |
| **SCR-20** | Content & AI Moderation Queue | `/admin/moderation` | AI NSFW scan confidence score breakdown, Moderator Approve/Reject panel. | [admin/scr20_content_moderation_queue_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/admin/scr20_content_moderation_queue_preview.html) |
| **SCR-21** | Creator Application Review | `/admin/creator-verifications` | Pending creator application queue, portfolio review, identity check. | [admin/scr21_creator_application_review_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/admin/scr21_creator_application_review_preview.html) |
| **SCR-22** | Dispute Resolution & Arbitration | `/admin/disputes/[id]` | Workroom chat history snapshot, evidence review, atomic Escrow refund slider (`0% - 100%`). | [admin/scr22_dispute_arbitration_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/admin/scr22_dispute_arbitration_preview.html) |
| **SCR-23** | User Account Sanctions | `/admin/users` | Account search, role permissions, suspension & ban modals. | [admin/scr23_user_management_sanctions_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/admin/scr23_user_management_sanctions_preview.html) |
| **SCR-18** | Platform Fee & Policy Config | `/admin/finance-settings` | Global platform fee rate slider (5% - 10%), refund rules, payout policy. | [admin/scr18_platform_fee_policy_preview.html](file:///d:/SEP490_Final/Web/dil-backend/docs/admin/scr18_platform_fee_policy_preview.html) |
