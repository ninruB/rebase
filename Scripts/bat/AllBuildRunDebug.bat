@echo off
cd ../../

call git submodule update --init --recursive
call dotnet build -c Debug
cd Scripts\bat
start runQuickServer.bat %*
start runQuickClient.bat %*
pause

