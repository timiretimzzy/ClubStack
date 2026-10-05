@echo off
set ConnectionStrings__Default=Host=localhost;Port=5432;Database=clubstack;Username=clubstack;Password=Clubstack_Local_2026!;Pooling=true;Maximum Pool Size=20
set App__EnableDevelopmentSeed=true
set App__EnableBootstrap=true
set App__DevelopmentAdminUserName=admin@localhost
set App__DevelopmentAdminPassword=GolfAdmin2026!
cd /d C:\Users\TIMIRE\Desktop\Club Management\ClubStack
start /b dotnet exec src\ClubStack.Api\bin\Debug\net10.0\ClubStack.Api.dll --urls http://localhost:5000
