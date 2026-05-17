# Volume 6: AI-Native Electronic Design Automation

As of 2025, the state of the art in EDA is characterized by a fundamental shift toward AI-native and agentic design flows. Traditional heuristics and simulated annealing are being rapidly replaced by machine learning models capable of handling extreme complexity.

## 1. Agentic AI & Flow Orchestration

### "Level 5" Autonomous Design
The historical EDA workflow involved a human engineer manually running synthesis, analyzing timing logs, tweaking RTL, and running place-and-route.
Modern "Agentic AI" (multi-step reasoning models) treats EDA tools as its environment.
- **RTL Generation**: Large Language Models (LLMs) tuned on Verilog/VHDL generate the initial hardware description from a natural language spec.
- **Autonomous Debugging**: If verification fails, an AI agent automatically reads the waveform logs, isolates the failing logic, rewrites the RTL, and recompiles the testbench without human intervention.
- **Cross-Tool Orchestration**: Agents handle the data-flow between disparate tools (Logic Synthesis $\rightarrow$ Floorplanning $\rightarrow$ Physical Verification), automatically adjusting constraints until PPA (Power, Performance, Area) targets are met.

## 2. Reinforcement Learning (RL) in Physical Design

Placing millions of logic macros (SRAM blocks, multipliers) on a 2D silicon canvas to minimize wirelength and congestion is an NP-Hard problem. Traditional algorithms (simulated annealing, quadratic placement) are slow and get trapped in local minima.

### Proximal Policy Optimization (PPO) for Placement
Reinforcement Learning models the chip canvas as a game board.
- **State**: The current location of all macros, the netlist graph, and routing congestion heatmaps.
- **Action**: Placing the next macro or adjusting the location of an existing one.
- **Reward**: A dense reward function based on Total Negative Slack (TNS), estimated wirelength (Half-Parameter Wirelength - HPWL), and routing congestion.
The RL agent learns a generalized policy over millions of simulated designs, allowing it to floorplan an entirely new, unseen CPU architecture in hours rather than weeks, heavily outperforming human experts.

### Analog Transistor Sizing
Analog design has historically been "black magic". RL is now used to automatically size the width ($W$) and length ($L$) of analog transistors (like in an Op-Amp or PLL) to hit specific gain, bandwidth, and phase margin targets, bypassing manual SPICE iterations.

## 3. Graph Neural Networks (GNNs)

A netlist is inherently a graph: logic gates are nodes, and wires are edges. GNNs are uniquely suited to analyze this topology.

### Timing and Congestion Prediction
Running a full physical place-and-route to see if a chip meets 5 GHz timing takes days.
Instead, a GNN analyzes the raw gate-level netlist (before physical placement). By passing message vectors along the graph edges, the GNN learns structural motifs that are prone to congestion or long delays.
- **Inference**: The GNN predicts the final parasitic capacitance and routing congestion with 95% accuracy in milliseconds, allowing the logic synthesis engine to restructure the RTL instantly without waiting for the physical design team.

## 4. Hybrid Symbolic-Neural Systems

Deep learning is probabilistic (it guesses), but semiconductor manufacturing requires 100% deterministic correctness. You cannot tape-out a 5nm chip that is "99% likely to be logically equivalent."

### Security-Aware Synthesis & Hardware Trojans
Hybrid systems blend neural networks (for fast pattern recognition) with symbolic logic (SAT solvers, Binary Decision Diagrams) for rigorous mathematical proofs.
- **Hardware Trojan Detection**: The neural network flags suspicious, rarely-toggled logic gates hidden in a massive SoC (potential malicious backdoors). The symbolic engine then mathematically proves whether that logic can be externally triggered.
- **Formal Verification**: LLMs generate formal SystemVerilog Assertions (SVA) from the design specification, and a deterministic formal verification engine rigorously proves that the RTL can never violate those assertions under any state-space combination.
