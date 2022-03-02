@echo off

IF NOT EXIST "nuspec/Silverlight.Controls.ToolTips.OpenSilver.nuspec" (
echo Wrong working directory. Please navigate to the folder that contains the BAT file before executing it.
PAUSE
EXIT
)



rem Define the escape character for colored text
for /F %%a in ('"prompt $E$S & echo on & for %%b in (1) do rem"') do set "ESC=%%a"

rem Define the "%PackageVersion%" variable:
set /p PackageVersion="%ESC%[92mSilverlight.Controls.ToolTips.OpenSilver version:%ESC%[0m 1.0.0-private-"

rem Get the current date and time:
for /F "tokens=2" %%i in ('date /t') do set currentdate=%%i
set currenttime=%time%

rem Create a Version.txt file with the date:
md temp
@echo Silverlight.Controls.ToolTips.OpenSilver 1.0.0-private-%PackageVersion% (%currentdate% %currenttime%)> temp/Version.txt

echo. 
echo %ESC%[95mRestoring NuGet packages%ESC%[0m
echo. 
nuget restore ../src/Silverlight.Controls.ToolTips.OpenSilver.sln -v quiet

echo. 
echo %ESC%[95mBuilding %ESC%[0mRelease %ESC%[95mconfiguration%ESC%[0m
echo. 
msbuild slnf/Silverlight.Controls.ToolTips.OpenSilver.slnf -p:Configuration=Release -clp:ErrorsOnly -restore
echo. 
echo %ESC%[95mPacking %ESC%[0mSilverlight.Controls.ToolTips.OpenSilver %ESC%[95mNuGet package%ESC%[0m
echo. 
nuget.exe pack nuspec\Silverlight.Controls.ToolTips.OpenSilver.nuspec -OutputDirectory "output/Silverlight.Controls.ToolTips.OpenSilver" -Properties "PackageId=Silverlight.Controls.ToolTips.OpenSilver;PackageVersion=1.0.0-private-%PackageVersion%;Configuration=Release;Target=Silverlight.Controls.ToolTips.OpenSilver;RepositoryUrl=https://github.com/OpenSilver/OpenSilver"

explorer "output\Silverlight.Controls.ToolTips.OpenSilver"

pause
