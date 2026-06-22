طريقة الاستخدام:
1) فك الضغط داخل جذر المشروع، نفس مكان DawishContentStudio.sln
2) شغل:
   powershell -ExecutionPolicy Bypass -File .\fix-manager-build-final.ps1
3) إذا نجح البناء، شغل:
   git push

ملاحظة:
إذا GitHub validate ما زال يظهر نفس السطر L134، فهذا يعني أن التعديل لم يُرفع للفرع الذي يشغّل validate.
