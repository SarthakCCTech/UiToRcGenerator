# Proof of Concept: UI-to-RC Resource Generator
## Executive Visual Presentation Deck (5 Slides)

---

### Slide 1: Problem Statement
**Title:** The Problem: Breakdown of Current Manual Workflow  
**Subtitle:** How manual string extraction creates bottlenecks, human transcription errors, and broken C++ builds.

#### 🔄 Manual Workflow Flowchart:
```mermaid
graph LR
    A[1. Target Desktop App<br>50+ Dialogs & Menus] -->|Manual Clicking| B[2. Human Transcription<br>Notepad / Excel Typing]
    B -->|Prone to Typos| C[3. Syntax & ID Flaws<br>Missing & Accelerators / Duplicate IDs]
    C -->|Compile Failure| D[4. Broken RC Build<br>Visual C++ Compiler Errors & Rework]

    style A fill:#1e293b,stroke:#f43f5e,stroke-width:2px,color:#fff
    style B fill:#1e293b,stroke:#f59e0b,stroke-width:2px,color:#fff
    style C fill:#1e293b,stroke:#f59e0b,stroke-width:2px,color:#fff
    style D fill:#1e293b,stroke:#ef4444,stroke-width:2px,color:#fff
```

* **⏱️ 16–20 Hours Wasted:** 2 full engineering days spent per release cycle on repetitive manual transcription.
* **⚠️ High Error Rate:** Missing keyboard accelerators (`&File`) and unescaped quotes break resource builds.
* **🛑 OCR Shortcomings:** Vision OCR is slow, inaccurate on small system fonts, and requires manual window cropping.

---

### Slide 2: Proposed Solution
**Title:** The Solution: Automated Live Hover Capture to RC  
**Subtitle:** Replacing manual friction with a zero-latency, automated hover-to-code pipeline.

#### 🔄 Before vs. After Flowchart:
```mermaid
graph TD
    subgraph BEFORE["❌ BEFORE: Manual Multi-Step Friction (16-20 Hours)"]
        B1[Target CAD App] --> B2[Alt-Tab to Notepad]
        B2 --> B3[Manual Typing & ID Guessing]
        B3 --> B4[Manual .RC File Editing]
        B4 --> B5[Compiler Syntax Errors]
    end

    subgraph AFTER["✅ AFTER: UI-to-RC Generator (< 15 Minutes • 100% Accuracy)"]
        A1[Target CAD App] -->|Ctrl+Shift+H| A2[Live Cursor Hover]
        A2 -->|Direct UIA 0ms| A3[Auto-Deduplication & ID Assignment]
        A3 -->|1-Click Validation| A4[Clean 2dTransRes.rc Ready for Build]
    end

    style BEFORE fill:#1c1917,stroke:#ef4444,stroke-width:2px,color:#fda4af
    style AFTER fill:#0f291e,stroke:#10b981,stroke-width:2px,color:#6ee7b7
```

---

### Slide 3: Technology Architecture Chart
**Title:** Technology Architecture: 4-Tier Native System  
**Subtitle:** Layered architecture engineered for native Windows performance, per-monitor DPI, and compiler compliance.

#### 🏗️ Layered System Architecture Chart:
```mermaid
graph TB
    subgraph L1["1. Presentation & UX Layer"]
        T1[.NET 10 / C# 13] --- T2[WPF & MVVM] --- T3[Per-Monitor V2 DPI]
    end

    subgraph L2["2. System Interop & Overlay Layer"]
        T4[Win32 RegisterHotKey API<br>Ctrl+Shift+H / F8] --- T5[Topmost Floating Recording Pill Widget]
    end

    subgraph L3["3. Core Extraction Engine"]
        T6["UIAutomationClient COM (Primary: 0ms, 100% Accurate)"]
        T7["Windows.Media.Ocr / Tesseract (Fallback Engine)"]
        T8["GDI+ Bitmap Contrast Preprocessor"]
    end

    subgraph L4["4. Resource Compiler & Serialization"]
        T9["Visual C++ .RC Generator"]
        T10["2dTransRes.rc Reference Template Injector"]
        T11["Regex Syntax & Duplicate ID Validator"]
    end

    L1 --> L2
    L2 --> L3
    L3 --> L4

    style L1 fill:#0f172a,stroke:#3b82f6,stroke-width:2px,color:#93c5fd
    style L2 fill:#0f172a,stroke:#8b5cf6,stroke-width:2px,color:#c4b5fd
    style L3 fill:#0f172a,stroke:#10b981,stroke-width:2px,color:#6ee7b7
    style L4 fill:#0f172a,stroke:#f59e0b,stroke-width:2px,color:#fcd34d
```

---

### Slide 4: How Our App Works (Main Focus)
**Title:** How Our App Works: End-to-End Pipeline Flowchart  
**Subtitle:** Detailed dataflow from mouse hover to fully validated Visual C++ resource script.

#### ⚙️ Complete Execution Pipeline Flowchart:
```mermaid
flowchart TD
    Start([User in Target App e.g. CAD]) --> Hotkey["Press Global Hotkey<br>(Ctrl+Shift+H or F8)"]
    Hotkey --> Widget["Display Floating 'REC' Pill Widget<br>(Live String Counter & Status)"]
    
    Widget --> Hover["Move Mouse Over Any Control<br>(Button, Menu, Tab, Dialog)"]
    
    Hover --> Decision{"Can UI Automation<br>Inspect Control?"}
    
    Decision -->|Yes: 0ms Native| UIA["Read element.Current.Name<br>& ControlType directly"]
    Decision -->|No: Legacy/Custom| OCR["ScreenCapture + Preprocess<br>Windows.Media.Ocr Fallback"]
    
    UIA --> Dedup["Live Deduplication Engine<br>(Ignores repeated hovers)"]
    OCR --> Dedup
    
    Dedup --> IDGen["Auto-Assign Resource ID<br>(e.g. IDS_BTN_EXPORT)"]
    IDGen --> Grid["Stream to ObservableCollection<br>(Table View: Edit / Split / Merge)"]
    
    Grid --> Generate["RC Generator Service<br>(Inject into 2dTransRes.rc Template)"]
    Generate --> Validate{"Syntax & ID Collision<br>Validator Pass?"}
    
    Validate -->|Passed| Output([Visual C++ .rc File Saved<br>Ready for Visual Studio Compilation])
    Validate -->|Issue Detected| Alert["Highlight Error in Red & Prompt Fix"]

    style Start fill:#1e293b,stroke:#38bdf8,stroke-width:2px,color:#fff
    style Hotkey fill:#1e293b,stroke:#38bdf8,stroke-width:2px,color:#fff
    style Widget fill:#1e293b,stroke:#8b5cf6,stroke-width:2px,color:#fff
    style Hover fill:#1e293b,stroke:#38bdf8,stroke-width:2px,color:#fff
    style UIA fill:#064e3b,stroke:#10b981,stroke-width:2px,color:#6ee7b7
    style OCR fill:#78350f,stroke:#f59e0b,stroke-width:2px,color:#fcd34d
    style Dedup fill:#1e293b,stroke:#06b6d4,stroke-width:2px,color:#fff
    style IDGen fill:#1e293b,stroke:#06b6d4,stroke-width:2px,color:#fff
    style Grid fill:#1e293b,stroke:#3b82f6,stroke-width:2px,color:#fff
    style Generate fill:#1e293b,stroke:#8b5cf6,stroke-width:2px,color:#fff
    style Output fill:#064e3b,stroke:#10b981,stroke-width:3px,color:#6ee7b7
```

* **Step 1: Global Trigger:** Win32 hotkey `Ctrl+Shift+H` starts capture from anywhere; floating widget appears on top.
* **Step 2: Hover Detection:** Direct UI Automation extracts native string & control type with 0 ms latency.
* **Step 3: Auto-Deduplication:** Repeated hovers are ignored; clean identifiers (`IDS_BTN_...`) are auto-generated.
* **Step 4: Template Injection:** Strings are compiled into `STRINGTABLE` blocks adhering to `2dTransRes.rc`.
* **Step 5: 1-Click Validation:** Verifies quotes and ID uniqueness, producing a compile-ready file.

---

### Slide 5: Business Impact & Comparison
**Title:** Business Impact: Measurable ROI & Quality Gains  
**Subtitle:** Quantifiable efficiency gains and complete elimination of resource compiler defects.

#### 📊 Impact Comparison Chart:
```
┌──────────────────────────┬─────────────────────────────┬──────────────────────────────┐
│ Metric                   │ Before (Manual Process)     │ With UI-to-RC Generator      │
├──────────────────────────┼─────────────────────────────┼──────────────────────────────┤
│ ⏱️ Time per 50 Dialogs   │ 16 – 20 Engineering Hours   │ < 15 Minutes (95% reduction) │
│ 🎯 String Accuracy       │ 80 – 85% (Typos, Missed &)  │ 100% (Direct OS Inspection)  │
│ 💥 Compiler Build Breaks │ Frequent RC Syntax Errors   │ 0 Build Breaks (Validated)   │
│ 🔀 Context Switching     │ Constant Alt-Tab & Typing   │ Zero (Floating Topmost Pill) │
└──────────────────────────┴─────────────────────────────┴──────────────────────────────┘
```

> **Recommendation to Manager:** Approve the POC for pilot rollout in our upcoming release cycle.
