# v0.4.4 Test Fix

This release adjusts `MedicalClaimsGuard` to avoid false positives when short Arabic medical terms appear inside safe words such as `الموقع` or `المتجر`.

The guard still blocks explicit medical and therapeutic claims, including:

- يعالج
- يشفي
- مفيد للهضم
- يقوي المناعة
- ضغط / سكر / قولون / كحة
- treat / cure / medicine / pain / immunity

The failed tests in v0.4.3 were caused by over-matching the short word `الم` inside safe marketing text.
