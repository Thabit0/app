# إصلاح DawishContentStudio من الصفر

هذه الحزمة تصلح أخطاء GitHub Actions الحالية:

- `OpenFileDialog` ambiguous بين `System.Windows.Forms.OpenFileDialog` و `Microsoft.Win32.OpenFileDialog`.
- تحذير `CS1998` في `MainWindow.xaml.cs` الذي يظهر في validate.
- أي تضارب محتمل في `MessageBox.Show`.
- إضافة `System.IO` لو الكود يستخدم `Path` أو `File` أو `Directory`.

## الطريقة الأساسية

1. فك الضغط داخل جذر المشروع، نفس المكان الذي يحتوي على:

```text
DawishContentStudio.sln
```

بعد الفك ستجد:

```text
tools/fix-manager-wpf-dialogs.ps1
.github/workflows/validate.yml
```

2. افتح PowerShell في جذر المشروع وشغل:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\fix-manager-wpf-dialogs.ps1 -Build
```

3. إذا نجح البناء، ارفع التعديل:

```powershell
git status
git add src/DawishContentStudio.Manager/MainWindow.xaml.cs src/DawishContentStudio.Manager/DawishContentStudio.Manager.csproj tools/fix-manager-wpf-dialogs.ps1
git commit -m "Fix Manager build validation errors"
git push
```

4. شغل validate من GitHub من جديد.

## لو تبغى GitHub Actions يصلح قبل البناء تلقائيًا

انسخ الملف الموجود داخل الحزمة:

```text
.github/workflows/validate.yml
```

مكان workflow الحالي في مشروعك، ثم ارفعه:

```powershell
git add .github/workflows/validate.yml
git commit -m "Update validate workflow"
git push
```

## ملاحظة مهمة

إذا ظهر نفس الخطأ بعد الإصلاح بنفس السطر 134، فهذا يعني أن التعديل لم يصل إلى الفرع الذي يشغل GitHub عليه validate.
تأكد أنك تعمل push على نفس الفرع الذي عليه الفحص.
