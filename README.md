🐝 Bee Swarm Game

This .NET project simulates a dynamic, adversarial bee swarm environment using the MonoGame framework. It emphasizes replayability, realism, and player immersion through evolving AI and interactive swarm behavior inspired by nature.

⸻

🌐 Overview
	•	Simulation Core: Competing bee swarms evolve and battle in a procedurally generated world.
	•	Game Modes:
	•	Single Player: Face intelligent swarms adapting to your tactics.
	•	Multiplayer (Local/Online): Compete PvP, each player managing unique swarm attributes.
	•	Swarm Composition: Units adapt roles (e.g., gatherer, defender, scout) dynamically based on real-time stimuli and hive needs.

⸻

🧠 Swarm Behavior
	•	Adversarial AI: Leverages pheromone-mimicry, ambush logic, and emergent rules.
	•	Resource Control: Nodes produce nectar/pollen, enabling evolution and expansion.
	•	Evolution System: Use resources to unlock abilities (stealth, group attack, hive enhancements).

⸻

🌍 Environment & Obstacles
	•	Procedurally generated maps with moving hazards and terrain variability.
	•	Realistic foraging zones and enemy hive behavior inspired by natural patterns.

⸻

🎮 Interactive UI & Visualization Features (MonoGame)

🗺 Visual Game UI Enhancements
	•	Minimap Overlay: Displays territory control, hive status, and swarm icons.
	•	Swarm Indicators: Live unit count, aggression level, resource collection meters.
	•	Pheromone Trail Visuals: Display real-time decision paths and AI movement flows.
	•	Dynamic Camera Modes: Zoom between macro (tactical) and micro (unit-focused) perspectives.
	•	Hover Tooltips: Unit role, stats, and current task displayed on cursor interaction.

📊 Real-Time Data Visualizer

Track the simulation’s heartbeat using these interactive elements:
	•	Population Metrics:
	•	Pie charts for swarm role distribution
	•	Bar graphs for per-unit type counts across swarms
	•	Resource Flow:
	•	Line graphs showing nectar/pollen accumulation over time
	•	Rate of resource conversion into upgrades and units
	•	Territory Control:
	•	Heatmaps indicating zone dominance and conflict areas
	•	Graphs showing time-based control shifts
	•	Performance Metrics:
	•	Swarm growth rates, unit mortality, average swarm distance from hive
	•	Toggle all visualizations with hotkeys or UI panel for cleaner gameplay when needed.

⸻

🛠 Development Guidelines

Future features must focus on:
	•	Enhanced immersion: Realistic movement, sounds, and visual feedback
	•	Replayability: Procedural elements, randomized swarm personalities
	•	Strategic Clarity: Clear info overlays and decision-making cues

Roadmap Suggestions:
	•	Seasonal changes and weather effects
	•	AI tuning sliders for difficulty customization
	•	Replay system with metrics timeline
	•	Fog-of-war and communication upgrades
	•	Cross-platform multiplayer leaderboard integration

⸻


🧭 Developer Tasking Instructions (for Copilot Integration)

To implement the advanced gameplay and visualization features described above, follow this structured roadmap:
	1.	Visualization Framework Setup
	•	Integrate a compatible data visualization library (e.g., OxyPlot, LiveCharts).
	•	Create a DataVisualizerManager to handle rendering for pie charts, bar graphs, line charts, and heatmaps.
	2.	Swarm Simulation Metrics
	•	Extend swarm unit models to track:
	•	Role and status (active/idle)
	•	Nectar and pollen collected
	•	Continuously log and update swarm population and role distribution.
	3.	Resource & Territory Tracking
	•	Track:
	•	Resource collection per hive
	•	Conversion rates into upgrades/units
	•	Swarm presence in zones to generate heatmaps for control
	4.	UI Integration
	•	Add UI panels or hotkey toggles for visualizations.
	•	Overlay components:
	•	Role pie charts
	•	Resource graphs
	•	Heatmaps
	•	Swarm growth rates and mortality
	5.	Event Trigger System
	•	Use event listeners for:
	•	Unit role changes
	•	Hive status updates
	•	Combat engagements
	6.	Performance Considerations
	•	Enable toggling of visual elements for cleaner gameplay.
	•	Profile impact and optimize rendering.
	7.	Bonus Features
	•	Add multiplayer metrics overlays.
	•	Support animated graph transitions.
	•	Prepare for serialization of simulation history.

💡 Tip for Copilot: Implement each metric visualization in an isolated branch and validate independently before merging.

⸻
