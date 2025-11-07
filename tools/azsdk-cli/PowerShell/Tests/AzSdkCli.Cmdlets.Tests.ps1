BeforeAll {
    # Import the binary module
    $ModulePath = Join-Path $PSScriptRoot ".." "AzSdkCli.Cmdlets.psd1"
    Import-Module $ModulePath -Force
}

Describe "Test-AzSdkPackage Cmdlet" {
    BeforeEach {
        # Create test directory structure
        $TestRoot = Join-Path $TestDrive "test-package"
        New-Item -ItemType Directory -Path $TestRoot -Force | Out-Null
    }

    It "Should return false when package path does not exist" {
        $result = Test-AzSdkPackage -PackagePath "/nonexistent/path" -ErrorAction SilentlyContinue
        $result | Should -Be $false
    }

    It "Should return false when README.md is missing" {
        $TestRoot = Join-Path $TestDrive "test-package-no-readme"
        New-Item -ItemType Directory -Path $TestRoot -Force | Out-Null
        New-Item -ItemType File -Path (Join-Path $TestRoot "CHANGELOG.md") -Force | Out-Null
        
        $result = Test-AzSdkPackage -PackagePath $TestRoot 3>$null
        $result | Should -Be $false
    }

    It "Should return false when CHANGELOG.md is missing" {
        $TestRoot = Join-Path $TestDrive "test-package-no-changelog"
        New-Item -ItemType Directory -Path $TestRoot -Force | Out-Null
        New-Item -ItemType File -Path (Join-Path $TestRoot "README.md") -Force | Out-Null
        
        $result = Test-AzSdkPackage -PackagePath $TestRoot 3>$null
        $result | Should -Be $false
    }

    It "Should return true when both required files are present" {
        $TestRoot = Join-Path $TestDrive "test-package-valid"
        New-Item -ItemType Directory -Path $TestRoot -Force | Out-Null
        New-Item -ItemType File -Path (Join-Path $TestRoot "README.md") -Force | Out-Null
        New-Item -ItemType File -Path (Join-Path $TestRoot "CHANGELOG.md") -Force | Out-Null
        
        $result = Test-AzSdkPackage -PackagePath $TestRoot
        $result | Should -Be $true
    }
}

Describe "Get-AzSdkFiles Cmdlet" {
    BeforeEach {
        # Create test directory with files
        $TestRoot = Join-Path $TestDrive "test-files"
        New-Item -ItemType Directory -Path $TestRoot -Force | Out-Null
        New-Item -ItemType File -Path (Join-Path $TestRoot "file1.txt") -Force | Out-Null
        New-Item -ItemType File -Path (Join-Path $TestRoot "file2.cs") -Force | Out-Null
        New-Item -ItemType File -Path (Join-Path $TestRoot "file3.ps1") -Force | Out-Null
    }

    It "Should list all files when no pattern is specified" {
        $TestRoot = Join-Path $TestDrive "test-files"
        $result = Get-AzSdkFiles -Path $TestRoot
        $result.Count | Should -Be 3
    }

    It "Should filter files by pattern" {
        $TestRoot = Join-Path $TestDrive "test-files"
        $result = Get-AzSdkFiles -Path $TestRoot -Pattern "*.cs"
        $result.Count | Should -Be 1
        $result[0].Name | Should -Be "file2.cs"
    }

    It "Should return error when directory does not exist" {
        $result = Get-AzSdkFiles -Path "/nonexistent/directory" -ErrorAction SilentlyContinue
        $result | Should -BeNullOrEmpty
    }

    It "Should return empty array when no files match pattern" {
        $TestRoot = Join-Path $TestDrive "test-files"
        $result = Get-AzSdkFiles -Path $TestRoot -Pattern "*.xyz"
        $result | Should -BeNullOrEmpty
    }
}

Describe "New-AzSdkMetadata Cmdlet" {
    It "Should create metadata file with correct content" {
        $OutputPath = Join-Path $TestDrive "test-metadata.json"
        $result = New-AzSdkMetadata -PackageName "TestPackage" -Version "2.0.0" -OutputPath $OutputPath
        
        $result | Should -Be $OutputPath
        Test-Path $OutputPath | Should -Be $true
        
        $content = Get-Content $OutputPath -Raw | ConvertFrom-Json
        $content.packageName | Should -Be "TestPackage"
        $content.version | Should -Be "2.0.0"
        $content.tool | Should -Be "azsdk-cli"
        $content.toolVersion | Should -Be "1.0.0"
    }

    It "Should create metadata file with default output path in specified directory" {
        $outputPath = Join-Path $TestDrive "test-default-metadata.json"
        $result = New-AzSdkMetadata -PackageName "TestPkg" -Version "1.0.0" -OutputPath $outputPath
        
        Test-Path $outputPath | Should -Be $true
        $content = Get-Content $outputPath -Raw | ConvertFrom-Json
        $content.packageName | Should -Be "TestPkg"
    }

    It "Should include generated timestamp" {
        $OutputPath = Join-Path $TestDrive "test-metadata-timestamp.json"
        New-AzSdkMetadata -PackageName "Test" -Version "1.0" -OutputPath $OutputPath | Out-Null
        
        $content = Get-Content $OutputPath -Raw | ConvertFrom-Json
        $content.generatedAt | Should -Not -BeNullOrEmpty
        
        # Verify it's a valid datetime
        { [DateTime]::Parse($content.generatedAt) } | Should -Not -Throw
    }
}

Describe "Show-AzSdkHelp Cmdlet" {
    It "Should display help without errors" {
        { Show-AzSdkHelp } | Should -Not -Throw
    }
}

AfterAll {
    # Clean up
    Remove-Module AzSdkCli.Cmdlets -ErrorAction SilentlyContinue
}
