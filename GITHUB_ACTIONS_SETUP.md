# GitHub Actions CI/CD Setup with Git LFS

This repository uses GitHub Actions to automatically build and publish the IPGeoLocator application for multiple platforms using free GitHub-hosted runners, with Git Large File Storage (LFS) for handling build artifacts.

## Git LFS Configuration

Git Large File Storage (LFS) is enabled to handle large binary files efficiently, including:

### Tracked File Types
- **Executables**: `.exe`, `.dll`, `.so`, `.dylib`, `.app`
- **Archives**: `.zip`, `.tar.gz`, `.tgz`, `.tar.bz2`
- **Databases**: `.db`, `.sqlite`, `.sqlite3`
- **Packages**: `.nupkg`, `.snupkg`
- **Media files**: `.png`, `.jpg`, `.jpeg`, `.gif`, `.ico`, `.svg`
- **Documentation**: `.pdf`
- **Native libraries**: `.framework`, `.a`, `.lib`
- **Build artifacts**: `publish/*`, `test-publish/*`

### LFS Benefits
- **Reduced repository size**: Large files are stored separately
- **Faster clones**: Only file pointers are included in the main repository
- **Better performance**: Large files don't slow down Git operations
- **Version tracking**: Full version control for large files

## Workflow Overview

The CI/CD pipeline is defined in `.github/workflows/build-and-publish.yml` and provides the following functionality:

### Triggers

- **Push to main/master branch**: Builds the application for all platforms
- **Pull requests**: Builds and tests changes before merging
- **Tags starting with 'v'**: Creates a release with downloadable artifacts
- **Manual trigger**: Can be run manually via GitHub Actions UI

### Build Matrix

The workflow uses a matrix strategy to build for multiple platforms simultaneously:

- **Linux x64** (ubuntu-latest runner)
- **Windows x64** (windows-latest runner) 
- **macOS x64** (macos-latest runner)

### LFS Integration

Each job properly handles Git LFS:

1. **Checkout with LFS**: `checkout@v4` with `lfs: true`
2. **LFS setup**: `git lfs checkout` ensures all LFS files are available
3. **Artifact handling**: Build outputs are automatically tracked by LFS if they match configured patterns

### Build Process

For each platform, the workflow:

1. **Checkout code** using `actions/checkout@v4` with LFS support
2. **Setup Git LFS** and checkout all LFS files
3. **Setup .NET 9** using `actions/setup-dotnet@v4`
4. **Restore dependencies** with `dotnet restore`
5. **Build application** in Release configuration
6. **Run tests** (if any exist, continues on error)
7. **Publish application** as self-contained single-file executable
8. **Create platform-specific archive** (ZIP for Windows, TAR.GZ for Linux/macOS)
9. **Upload artifacts** with 30-day retention

### Release Process

When a tag starting with 'v' is pushed (e.g., `v1.0.0`):

1. All platform builds complete successfully
2. **Download all artifacts** from the build jobs
3. **Create GitHub release** with auto-generated release notes
4. **Attach platform-specific archives** as release assets

## Artifacts

### Build Artifacts

For every build, the following artifacts are generated and stored for 30 days:

- `IPGeoLocator-linux-x64.tar.gz`
- `IPGeoLocator-windows-x64.zip`  
- `IPGeoLocator-macos-x64.tar.gz`

### Release Assets

For tagged releases, the same files are permanently attached to the release.

## Platform-Specific Details

### Linux (ubuntu-latest)
- Runtime: `linux-x64`
- Archive: TAR.GZ format
- Self-contained executable with shared libraries
- LFS handles large `.so` files automatically

### Windows (windows-latest)  
- Runtime: `win-x64`
- Archive: ZIP format
- Self-contained executable (.exe)
- LFS handles large `.exe` and `.dll` files automatically

### macOS (macos-latest)
- Runtime: `osx-x64`
- Archive: TAR.GZ format
- Self-contained executable
- LFS handles large `.dylib` and `.framework` files automatically

## Build Configuration

The application is built with the following settings:

- **Configuration**: Release
- **Self-contained**: True (includes .NET runtime)
- **Single file**: True (packages into single executable)
- **Trimmed**: False (preserves full functionality)

## Git LFS Setup

### For Developers

When cloning the repository:

```bash
# Clone with LFS support
git clone <repository-url>
cd IPGeoLocator

# Install LFS (if not already installed)
git lfs install

# Pull all LFS files
git lfs pull
```

### Adding New File Types to LFS

To track additional file types with LFS:

```bash
# Track specific file types
git lfs track "*.extension"

# Track files in specific directories
git lfs track "path/to/directory/*"

# Add the .gitattributes file
git add .gitattributes
git commit -m "Track new file types with LFS"
```

### Checking LFS Status

```bash
# See which files are tracked by LFS
git lfs ls-files

# Check LFS status
git lfs status

# Show LFS configuration
git lfs env
```

## Free GitHub Resources

This workflow uses only free GitHub-provided resources:

- **GitHub-hosted runners**: ubuntu-latest, windows-latest, macos-latest
- **GitHub Actions**: Standard action marketplace actions
- **Artifact storage**: 30-day retention for build artifacts
- **Release storage**: Permanent storage for release assets
- **Git LFS**: Free quota for public repositories (1GB storage, 1GB bandwidth per month)

## Usage

### For Developers

1. **Push to main/master**: Triggers build and artifact generation
2. **Create pull request**: Triggers build to validate changes
3. **Push version tag**: Triggers release creation

### Creating a Release

To create a new release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

This will trigger the full build and release process.

### Manual Trigger

The workflow can also be triggered manually:

1. Go to repository Actions tab
2. Select "Build and Publish IPGeoLocator" workflow
3. Click "Run workflow"
4. Choose branch and click "Run workflow"

## Monitoring

You can monitor the workflow progress in:

- **Actions tab**: Shows all workflow runs
- **Individual runs**: Shows detailed logs for each job
- **Artifacts section**: Download build artifacts
- **Releases section**: View and download released versions
- **LFS usage**: Check in repository Settings > Git LFS

## Troubleshooting

### Common Issues

1. **Build failures**: Check the build logs in the Actions tab
2. **Missing artifacts**: Verify the build completed successfully
3. **Release creation fails**: Ensure the tag follows the `v*` pattern
4. **LFS quota exceeded**: Monitor usage in repository settings

### LFS-Specific Issues

1. **Large files not uploading**: Check `.gitattributes` configuration
2. **LFS quota exceeded**: Upgrade plan or clean up old LFS files
3. **Slow checkouts**: Large LFS files take time to download

### Build Warnings

The current build produces warnings related to:
- Duplicate using statements (non-critical)
- Nullable reference warnings (non-critical)
- Obsolete API usage (non-critical)

These warnings do not affect functionality and the application builds successfully.

## Cost

This CI/CD setup uses GitHub's free tier resources:

- **GitHub Actions minutes**: Free for public repositories
- **Artifact storage**: Free with 30-day retention
- **Release storage**: Free and permanent
- **Runner resources**: Provided by GitHub at no cost
- **Git LFS**: Free quota for public repositories

For private repositories, this would consume paid GitHub Actions minutes and LFS quota according to your plan.

## Security

The workflow follows security best practices:

- **Minimal permissions**: Each job has only the permissions it needs
- **LFS security**: Large files are stored securely by GitHub
- **No secrets exposure**: Build process doesn't expose sensitive data
- **Artifact retention**: Limited to 30 days to reduce storage usage