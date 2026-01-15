# 🔫 Spacegun Simulator - pre-alpha

**A command-line engineering simulation game about building the biggest gun in human history — and getting the maths right.**

## Playtesting

Actively seeking testers interested in:
- Physics-based gameplay
- Engineering challenges
- Command-line / ASCII games

Feel free to open an issue or start a discussion. Advise or corrections are esspecially valued.

---

## 🛰️ Overview

**Spacegun Simulator** is a **terminal-based physics and engineering simulation game** set in an alternate 1930s–1940s timeline.

Humanity has detected a massive ballistic object on a near-relativistic trajectory toward Earth.  
It cannot be deflected. It cannot be reasoned with.  
There is only one option:

**Build a gun large enough to destroy it.**

There will be **one shot per wave**.  
One miscalculation means extinction.

No Automation.  
No computers.  
Just charts, mechanical calculators, and your ability to solve real physics problems under pressure.

nb. Any input, esp maths corrections, would be greatly appreciated.

---

## ⚙️ Core Pillars

- **Real physics calculations** (no hidden RNG at the point of firing)
- **Command-line interface** with ASCII art and page-based UI
- **Procedural systems** for targets, resources, and technology paths 
- **High-stakes permadeath** (single save, no save scumming)
- **Procedural lo-fi / dieselpunk audio** to reinforce tension and pacing

---

## 🕰️ Narrative Setting

**Alternate Timeline – 1930s**

Long-range sensors detect a projectile inbound toward Earth at extreme velocity.  
Its size, density, and trajectory are known. Its arrival time is certain.

The world lays down its arms.

Instead of computing machines and silicon processors, humanity invests everything into:

- Mechanical engineering  
- Structural science  
- Ballistics  
- Analog computation  

The digital age never happens.

When the moment arrives, you will have:

- Mechanical calculators  
- Ballistic charts  
- Trajectory tables  
- Radar thresholds  

You will **not** have:
  
- Computers  
- Automated targeting  

Hitting an object the size of a bus, moving at thousands of kilometers per hour, is entirely on you.

---

## 🖥️ Aesthetic

- **retro-futurism**
- Benign dystopia
- Heavy industry, rivets, steel, slide rules
- Stark ASCII diagrams and schematic-style layouts
- Functional, utilitarian UI

---

## 🎮 Gameplay Loop

### 1️⃣ Detection Phase

A new target is generated with known physical parameters:

- Trajectory  
- Mass  
- Radar cross section (affects targeting tolerance)  
- Velocity  
- ETA (time until impact)  

This information is **truthful and complete** — success depends on interpretation, not guessing.

---

### 2️⃣ Resource Phases

**Time is the primary currency.**

The time until impact determines how many actions you can take.

Time is spent on:

- Collecting resources  
- Researching new technologies (in development)
- Unlock new projectile designs (in development)

Spending time is always a tradeoff.

---

### 3️⃣ Development Phase

Use gathered resources to:


- Manufacturing projectile components  (in development)
- Reinforcing or repairing the gun  (in development)
- Improve barrel strength, tolerances, and recoil handling  (in development)
- Push engineering limits closer to required kinetic energy  

Technology is deterministic — but choosing *what* to develop matters.

---

### 4️⃣ Firing Phase (The Moment)

Using only the provided tools:

- Trajectory charts  
- Ballistic tables  
- Mechanical calculators  

You must determine:

- Exact firing time  
- Elevation  
- Azimuth  
- Muzzle velocity  

Then you fire.

There is no correction shot.  
There is no second chance.

---

## ✅ Success & Failure

- **Success:**  
  Proceed to the next wave (25 total waves)

- **Failure:**  
  Save file is deleted  
  The world ends  
  Start again  

---

## ⚠️ Difficulty Philosophy

Difficulty scales **numerically**, not artificially.

- Tutorial levels may require **1 decimal place** of precision  
  *(potato cannon vs beachball)*

- Late-game levels demand **6 decimal places** of accuracy  
  *(bus-sized target at relativistic speed)*

The game does not cheat.  
The maths always (?) works.  
Failure is always explainable.

---

## 💾 Saving & Permadeath

- Auto-save on every page load  
- **One save slot**  
- No manual saving  
- No save scumming  
- Game over deletes the save  

---

## 🎧 Audio

Spacegun Simulator includes a deeply customisable **procedural, lo-fi soundscape** synth:

- Drum loops and procedural, mechanical rhythms  
- Melody seed editor
- Customisable drumloop library
- Many filters

---

## 🛠️ Technology

- **Language:** C#  
- **Platform:** .NET  
- **Interface:** Command-line / Terminal  
- **Audio:** Procedural synthesis (NAudio)  
- **Persistence:** Single-save, auto-save only  

---

## Development Notes

AI tools were used during development for prototyping, refactoring, and exploring mathematical
approaches.

All core systems, rules, and calculations are intentionally designed, reviewed, and constrained.
The game relies on deterministic, explainable mechanics rather than opaque automation.

### Headless / Diagnostics Flags

These are useful for balancing work and quick regression checks.

- `--consistency-check` runs internal validation checks without launching the UI.
- `--tuninglab-smoke` runs quick tuning smoke tests without launching the UI.
- `--test-campaign` runs a fast, headless “autoplay” campaign that auto-allocates years, auto-researches any affordable tech, and forces a successful shot each wave (still consumes barrel wear).

Examples:

```bash
dotnet run --project "Spacegun Simulator/SpacegunSimulator.csproj" -c Release -- --test-campaign --waves 25 --seed 12345
```

Optional args:

- `--waves N` limits the number of waves to simulate.
- `--seed N` makes the run deterministic.
- `--mode <GameModeId>` selects the game mode by enum name.

---

## Licensing

Source code is released under the MIT License.

Narrative content, game identity, and non-code assets are not covered by the
MIT License. Audio assets are included under their respective source licenses
and attribution requirements.

See `LICENSE` and `ASSETS.md` for details.
