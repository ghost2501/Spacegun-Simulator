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

nb. There is still a lot to do. The tech paths are very nascent and general balancing has barely begun. Any input, esp maths corrections, would be greatly appreciated.

---

## ⚙️ Core Pillars

- **Real physics calculations** (no hidden RNG at the point of firing)
- **Command-line interface** with ASCII art and page-based UI
- **Procedural systems** for targets, resources, and technology paths (in development)
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

- **Dieselpunk / 1940s retro-futurism**
- Benign dystopia
- Heavy industry, rivets, steel, slide rules
- Stark ASCII diagrams and schematic-style layouts
- Functional, utilitarian UI — nothing ornamental

The terminal is not a limitation.  
It is the control room.

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
The maths always works.  
Failure is always explainable.

---

## 💾 Saving & Permadeath

- Auto-save on every page load  
- **One save slot**  
- No manual saving  
- No save scumming  
- Game over deletes the save  

This enforces:

- Careful planning  
- Respect for time  
- Emotional weight behind each decision  

---

## 🎧 Audio

Spacegun Simulator includes a **procedural, lo-fi soundscape**:

- Non-looping  (ish)
- Reactive to game state  (not yet implemented)
- Designed to pace thinking, not distract  (Your milage may vary)
- Drums and mechanical rhythms dominate  
- Harmony is atmospheric, not melodic  

The music functions as **emotional UI**, not background noise.

---

## 🧪 What This Game Is (and Isn’t)

**This is not:**

- A text adventure  
- A visual simulation  
- A twitch-based game  

**This *is*:**

- An engineering puzzle  
- A physics simulator  
- A high-stakes planning game  
- A command-line game that treats the terminal as a strength  

---

## 🛠️ Technology

- **Language:** C#  
- **Platform:** .NET  
- **Interface:** Command-line / Terminal  
- **Audio:** Procedural synthesis (NAudio)  
- **Persistence:** Single-save, auto-save only  

---

## 🧭 Final Note

**Spacegun Simulator** is about responsibility.

When the time comes, the calculations will be correct — or they won’t.  
The universe does not care which.

---

## Development Notes

AI tools were used during development for prototyping, refactoring, and exploring mathematical
approaches.

All core systems, rules, and calculations are intentionally designed, reviewed, and constrained.
The game relies on deterministic, explainable mechanics rather than opaque automation.

AI is treated as a development aid, not an authority.

---

## Licensing

Source code is released under the MIT License.

Narrative content, game identity, and non-code assets are not covered by the
MIT License. Audio assets are included under their respective source licenses
and attribution requirements.

See `LICENSE` and `ASSETS.md` for details.
