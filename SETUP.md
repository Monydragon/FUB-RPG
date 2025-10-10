# Quick Setup & Build Guide

## SDK Issue Resolution

If you encounter SDK errors when building, try one of these solutions:

### Option 1: Update global.json (Recommended)
Remove or update the `global.json` file to use your installed SDK version:

```bash
# Check your installed SDK version
dotnet --list-sdks

# Then either delete global.json or update it to match your SDK
```

### Option 2: Install the Required SDK
Download and install .NET SDK from:
https://dotnet.microsoft.com/download

### Option 3: Allow SDK Rollforward
Update `global.json` to allow any newer SDK:

```json
{
  "sdk": {
    "version": "8.0.0",
    "rollForward": "latestMajor",
    "allowPrerelease": true
  }
}
```

## Build & Run

```bash
# Navigate to the project directory
cd D:\Projects\Console\FubWithAgents

# Restore dependencies
dotnet restore FubWithAgents\FubWithAgents.csproj

# Build the project
dotnet build FubWithAgents\FubWithAgents.csproj

# Run the game
dotnet run --project FubWithAgents\FubWithAgents.csproj
```

## Quick Test

Once running, you should see:
1. Character creation for party members
2. Species and class selection
3. A generated world with 3 maps (Town, Forest, Dungeon)
4. Exploration menu with movement and actions
5. Combat encounters with turn-based actions
6. Equipment and job level tracking

Enjoy your adventure!

