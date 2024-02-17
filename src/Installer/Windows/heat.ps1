# PowerShell script to generate WiX Component entries for files in a directory

$sourceDirectory = "../../server/wwwroot"
$outputFile = "wwwroot.wxi" # Update this path to your output file

# Function to generate a GUID
function New-Guid() {
    return [guid]::NewGuid().ToString().ToUpper()
}

# Function to create a valid WiX Id by removing invalid characters and ensuring it does not start with a digit
function Create-WixId([string]$name) {
    $validId = $name -replace '[^A-Za-z0-9_]', '' -replace '^(?=\d)', '_'
    return $validId
}

function Get-SourcePath([string]$file) {
    # Substring to find the last occurrence of
    $substring = "wwwroot"

    # Find the last index of the substring
    $lastIndex = $file.LastIndexOf($substring)

    # Check if the substring was found
    if ($lastIndex -ne -1) {
        # If you want to remove everything AFTER the substring,
        # add the length of the substring to the index
        $newString = $file.Substring($lastIndex, $file.Length - $lastIndex)
    }
    else {
        # Substring not found, optional handling
        $newString = $file
    }

    return $newString
}

# Function to create WiX Component elements for files
function Create-WixFileComponent($file, $parentId) {
    $fileId = Create-WixId $file.Name
    $guid = New-Guid
    $relativePath = Get-SourcePath $file.FullName
    return @"

    <Component Id="$fileId" Guid="$guid">
        <File Id="file_$fileId" Source="`$(var.SourcePath)\$relativePath" KeyPath="yes" />
    </Component>
"@
}

# Function to recursively create WiX Directory and Component elements
function Create-WixElements($directories, $parentId) {
    $wixElements = ""
    foreach ($dir in $directories) {
        $dirId = Create-WixId $dir.Name
        $dirName = $dir.Name
        if ($parentId) {
            $dirId = "$parentId.$dirId" # Ensure unique ID for nested directories
        }
        $wixElements += @"

    <Directory Id="$dirId" Name="$dirName">
"@
        $files = Get-ChildItem -Path $dir.FullName -File
        foreach ($file in $files) {
            $wixElements += Create-WixFileComponent $file $dirId
        }
        $subDirs = Get-ChildItem -Path $dir.FullName -Directory
        if ($subDirs) {
            $wixElements += Create-WixElements $subDirs $dirId
        }
        $wixElements += "    </Directory>`n"
    }
    return $wixElements
}

# Initialize WiX file content with header
$wixContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<?define SourcePath="..\..\..\publish\standalone\win-x64" ?>
<Include xmlns="http://wixtoolset.org/schemas/v4/wxs">
"@

# Create the Directory element for the sourceDirectory itself
$wwwrootDirName = Split-Path -Leaf $sourceDirectory
$wwwrootDirId = Create-WixId $wwwrootDirName
$wixContent += @"
    <Directory Id="$wwwrootDirId" Name="$wwwrootDirName">
"@

# Create components for files directly in sourceDirectory
$rootFiles = Get-ChildItem -Path $sourceDirectory -File
foreach ($file in $rootFiles) {
    $wixContent += Create-WixFileComponent $file $wwwrootDirId
}

# Generate Directory and Component elements for subdirectories
$rootDirs = Get-ChildItem -Path $sourceDirectory -Directory
$wixContent += Create-WixElements $rootDirs $wwwrootDirId

# Close the Directory for sourceDirectory and the WiX file content with footer
$wixContent += @"
    </Directory>
</Include>
"@

# Output the content to the file
$wixContent | Out-File -FilePath $outputFile -Encoding UTF8

Write-Host "WiX file generated at $outputFile"
