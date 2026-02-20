# 5-Week REST API Curriculum: A Standards-Aligned Lesson Plan for Educators

You just read [Teaching REST APIs Through Gaming](/Blog/Post/teaching-rest-apis-through-gaming) and thought, *"That sounds great, but how do I actually use this in my classroom?"*

Here's the answer: a complete, standards-aligned lesson plan with an assessment rubric you can hand to your department chair today.

## Course Overview

| | |
|---|---|
| **Course** | Introduction to REST APIs Through Competitive Programming |
| **Grade Level** | 9–12 / Post-secondary |
| **Duration** | 5 weeks (25 instructional hours) |
| **Prerequisites** | Basic programming concepts (variables, loops, conditionals) |
| **Language** | Any (Python recommended for beginners) |
| **Tools Required** | Text editor, terminal/command line, internet access |
| **Platform** | [API Combat](https://apicombat.com) — Education Mode (free for accredited institutions) |

## Wisconsin Standards for Computer Science Alignment

This curriculum maps directly to the [Wisconsin Standards for Computer Science](https://dpi.wi.gov/computer-science/wisconsin-standards) at the 9–12 grade band. Standards marked with (+) represent advanced expectations for students pursuing computing careers or postsecondary study.

### Algorithms & Programming

| Standard | Description | Weeks |
|----------|-------------|-------|
| **AP1.a.8.h** | Analyze a problem and design and implement an algorithmic solution using sequence, selection, and iteration | 2–5 |
| **AP1.a.14.h** (+) | Develop and use a series of test cases to verify that a program performs according to its design specifications | 3–4 |
| **AP2.a.13.h** (+) | Decompose a computational problem by creating new data types, functions, or classes | 3–5 |
| **AP2.a.16.h** (+) | Demonstrate code reuse by creating programming solutions using libraries and application program interfaces (APIs) | 2–5 |
| **AP3.c.5.h** (+) | Use application programming interface (API) documentation resources | 1–5 |
| **AP3.c.6.h** | Use online resources to answer technical questions | 1–5 |
| **AP4.a.6.h** | Deconstruct a complex problem into simpler parts using predefined constructs (e.g., functions and parameters) | 2–4 |
| **AP5.a.6.h** | Design and develop a software artifact working in a team | 4–5 |
| **AP5.a.9.h** (+) | Use version control systems, integrated development environments (IDEs), and collaboration tools and practices in a group software project | 4–5 |
| **AP6.a.4.h** | Use a systematic approach and debugging tools to independently debug a program | 2–5 |

### Networks & the Internet

| Standard | Description | Weeks |
|----------|-------------|-------|
| **NI2.b.3.h** | Describe key protocols and underlying processes of internet-based services (e.g., HTTP/HTTPS, SMTP/IMAP, routing protocols) | 1–2 |
| **NI2.d.5.h** (+) | Explore security policies by implementing and comparing encryption and authentication strategies (e.g., secure coding, safeguarding keys) | 1–3 |

### Data & Analysis

| Standard | Description | Weeks |
|----------|-------------|-------|
| **DA2.a.4.h** | Discuss techniques used to store, process, and retrieve different amounts of information (e.g., files, databases, data warehouses) | 3–4 |
| **DA3.a.6.h** | Use computational tools to collect, transform, and organize data about a problem to explain to others | 3–5 |

### Impacts of Computing

| Standard | Description | Weeks |
|----------|-------------|-------|
| **IC2.c.5.h** | Ethically and safely select, observe, and contribute to global collaboration in the development of a computational artifact | 4–5 |

---

## Week-by-Week Lesson Plans

### Week 1: API Fundamentals — Manual API Calls

**Learning Objectives:**
- Explain what a REST API is and why it matters in modern software
- Construct HTTP requests using curl or Postman
- Authenticate with a remote service using Bearer tokens
- Parse JSON responses and identify data structures

**Activities:**

| Day | Activity | Details |
|-----|----------|---------|
| 1–2 | Introduction to HTTP and REST | Instructor-led demo: making API calls with curl. Students register an API Combat account via the API itself — their first POST request. |
| 3 | JSON Deep-Dive | Students examine API responses. Identify objects, arrays, nested fields. Map JSON structure to real-world concepts (roster = array of unit objects). |
| 4–5 | First Battle | Students authenticate, retrieve their roster (`GET /v1/player/roster`), and queue their first battle. **Journal entry:** document every request/response pair with annotations. |

**Deliverable:** Annotated log of a completed battle showing request method, endpoint, headers, payload, and response for each API call made.

**Standards Addressed:** AP3.c.5.h, AP3.c.6.h, NI2.b.3.h, NI2.d.5.h

---

### Week 2: Automation — Build a Bot

**Learning Objectives:**
- Write a script that makes HTTP requests programmatically
- Implement loops and conditionals for iterative API interaction
- Handle error responses from a remote API
- Log structured results to a file for later analysis

**Activities:**

| Day | Activity | Details |
|-----|----------|---------|
| 1 | From curl to code | Introduction to the `requests` library (Python) or equivalent. Students convert their manual curl commands into a script. |
| 2–3 | Build the bot | Write a bot that auto-queues 10 battles and logs results: wins, losses, error codes, timestamps. |
| 4–5 | Error handling workshop | Introduce HTTP status codes (400, 401, 403, 429, 500). Students add retry logic for rate limits (`429 Too Many Requests`). Discuss exponential backoff. |

**Deliverable:** Working bot script that completes 10 battles autonomously with error handling and a structured log file of results.

**Standards Addressed:** AP1.a.8.h, AP2.a.16.h, AP4.a.6.h, AP6.a.4.h, NI2.d.5.h

---

### Week 3: Strategy & Optimization

**Learning Objectives:**
- Create and upload strategy configurations via the API
- Design controlled experiments to compare approaches
- Apply basic data analysis to optimize outcomes
- Interpret rate-limiting headers and implement request throttling

**Activities:**

| Day | Activity | Details |
|-----|----------|---------|
| 1–2 | Strategy deep-dive | Students study the strategy JSON schema. Create 3 distinct formations using `POST /v1/strategy/create` and `PUT /v1/strategy/update`. |
| 3 | A/B testing workshop | Run each strategy through 20+ battles. Record win rates per strategy. Discuss what constitutes a meaningful sample size. |
| 4–5 | Data analysis | Calculate win rates, compare formations, identify patterns. **Written analysis:** which strategy performs best and *why*? Support conclusions with data. |

**Deliverable:** Three strategy JSON configurations, raw battle results data, and a written analysis (500–800 words) with win-rate calculations and evidence-based conclusions.

**Standards Addressed:** AP1.a.8.h, AP1.a.14.h, AP2.a.13.h, DA2.a.4.h, DA3.a.6.h

---

### Week 4: Advanced Features & Collaboration

**Learning Objectives:**
- Use batch operations and simulation endpoints for large-scale data collection
- Aggregate and analyze datasets with 100+ data points
- Collaborate with teammates using version control
- Contribute to a shared software project using professional tools

**Activities:**

| Day | Activity | Details |
|-----|----------|---------|
| 1–2 | Simulation endpoint | Introduction to batch operations. Students run 100+ simulated battles and collect structured data. |
| 3 | Statistical analysis workshop | Calculate mean, median, and standard deviation of battle outcomes. Create visualizations (charts, tables). |
| 4–5 | Team formation (Guilds) | Form teams of 3–4 students. Set up a shared GitHub repository. Share strategies, coordinate guild war tactics, conduct peer code review. |

**Deliverable:** Simulation report with data visualizations (minimum 100 battles analyzed), plus a team GitHub repository with shared strategies and documented commit history.

**Standards Addressed:** AP2.a.13.h, AP5.a.6.h, AP5.a.9.h, DA3.a.6.h, IC2.c.5.h

---

### Week 5: Tournament & Presentation

**Learning Objectives:**
- Optimize a fully autonomous bot for competitive performance
- Present technical findings and decisions to peers
- Evaluate and reflect on the iterative development process
- Demonstrate collaborative software development practices

**Activities:**

| Day | Activity | Details |
|-----|----------|---------|
| 1–2 | Final optimization | Teams refine strategies, improve error handling, add advanced features. Final bot must run fully autonomously. |
| 3 | Class tournament | Bots compete on the live leaderboard. Real-time results projected for the class. |
| 4–5 | Presentations & reflection | Each team presents their approach (5–10 minutes): architecture decisions, what worked, what failed, what they'd do differently. Peer feedback. Individual reflection essay. |

**Deliverable:** Tournament-ready autonomous bot, team presentation (5–10 minutes with slides or live demo), and an individual reflection essay (300–500 words).

**Standards Addressed:** AP1.a.14.h, AP5.a.6.h, AP5.a.9.h, DA3.a.6.h, IC2.c.5.h

---

## Assessment Rubric

### Performance Levels

| Level | Score | Description |
|-------|:-----:|-------------|
| **Exceeds Expectations** | 4 | Demonstrates mastery beyond requirements. Independently extends concepts and applies them in novel ways. |
| **Meets Expectations** | 3 | Demonstrates solid understanding. Meets all requirements with only minor gaps. |
| **Approaching Expectations** | 2 | Demonstrates partial understanding. Meets some requirements, often with instructor support. |
| **Beginning** | 1 | Demonstrates limited understanding. Significant gaps in meeting requirements. |

### Scoring Criteria

#### 1. API Fundamentals & HTTP Protocol — 20%
*Standards: NI2.b.3.h, NI2.d.5.h, AP3.c.5.h*

| Exceeds (4) | Meets (3) | Approaching (2) | Beginning (1) |
|-------------|-----------|------------------|---------------|
| Correctly constructs all HTTP method types (GET, POST, PUT, DELETE). Explains REST constraints and authentication flows unprompted. Independently troubleshoots auth issues and token expiration. | Correctly uses GET, POST, PUT, DELETE with appropriate payloads. Authenticates using Bearer tokens. Explains the purpose of each HTTP method. | Uses 2–3 HTTP methods correctly. Authenticates with instructor support. Partially explains method purposes. | Struggles to construct HTTP requests. Cannot authenticate independently. Limited understanding of the request/response cycle. |

#### 2. Programming & Automation — 25%
*Standards: AP1.a.8.h, AP2.a.16.h, AP4.a.6.h*

| Exceeds (4) | Meets (3) | Approaching (2) | Beginning (1) |
|-------------|-----------|------------------|---------------|
| Bot runs fully autonomously with robust error handling, exponential backoff, and structured logging. Code is well-organized with functions or classes and handles edge cases gracefully. | Bot completes 10+ battles automatically. Includes basic error handling and file logging. Code uses functions and loop constructs appropriately. | Bot runs but requires some manual intervention. Some error handling present. Code works but lacks clear structure or organization. | Bot does not run reliably. Minimal or no error handling. Code is incomplete or non-functional without significant assistance. |

#### 3. Data Analysis & Optimization — 20%
*Standards: DA2.a.4.h, DA3.a.6.h, AP1.a.14.h*

| Exceeds (4) | Meets (3) | Approaching (2) | Beginning (1) |
|-------------|-----------|------------------|---------------|
| Analyzes 100+ simulations using statistical methods (mean, median, standard deviation). Creates clear data visualizations. Draws evidence-based conclusions with specific, actionable recommendations. | Runs 20+ battles per strategy. Calculates win rates accurately. Identifies best-performing strategy with supporting data and a written explanation. | Compares 2 strategies with limited data (<20 battles). Identifies a preference but lacks quantitative support for conclusions. | Does not collect meaningful data. No comparison between strategies. Conclusions are absent or entirely unsupported by evidence. |

#### 4. Collaboration & Version Control — 15%
*Standards: AP5.a.6.h, AP5.a.9.h, IC2.c.5.h*

| Exceeds (4) | Meets (3) | Approaching (2) | Beginning (1) |
|-------------|-----------|------------------|---------------|
| Active contributor with meaningful, well-documented commits. Reviews teammates' code constructively. Manages merge conflicts. Demonstrates leadership in guild coordination and strategy. | Commits code regularly to shared repository. Collaborates effectively on team tasks. Participates in guild activities and peer review. | Limited commits to shared repository. Some participation in team activities. Requires prompting to collaborate or share work. | Does not use version control meaningfully. Minimal team participation. Does not contribute to shared artifacts or guild coordination. |

#### 5. Communication & Presentation — 10%
*Standards: AP5.a.6.h*

| Exceeds (4) | Meets (3) | Approaching (2) | Beginning (1) |
|-------------|-----------|------------------|---------------|
| Presentation is clear, well-organized, and technically precise. Explains design trade-offs and demonstrates deep understanding of choices made. Answers audience questions confidently. | Presents findings clearly with logical organization. Explains approach and results. Responds to basic questions from peers and instructor. | Presentation covers required topics but lacks depth or clear organization. Difficulty answering follow-up questions. | Presentation is incomplete or unclear. Cannot explain approach, results, or technical decisions made during the project. |

#### 6. Error Handling & Debugging — 10%
*Standards: AP6.a.4.h, NI2.d.5.h*

| Exceeds (4) | Meets (3) | Approaching (2) | Beginning (1) |
|-------------|-----------|------------------|---------------|
| Implements comprehensive error handling for all common HTTP status codes. Uses a systematic debugging approach (read error, form hypothesis, test fix). Documents error resolution process in code comments or log. | Handles common errors (400, 401, 429). Debugs most API issues independently. Implements basic retry logic for rate limits. | Handles 1–2 error types. Requires some instructor support for debugging unfamiliar errors. | No meaningful error handling. Cannot debug API errors without significant step-by-step assistance. |

### Grading Scale

| Weighted Score | Grade |
|:--------------:|:-----:|
| 3.5 – 4.0 | A |
| 3.0 – 3.49 | B |
| 2.5 – 2.99 | C |
| 2.0 – 2.49 | D |
| Below 2.0 | F |

*Each criterion is scored 1–4, then multiplied by its weight. Final score is the sum of all weighted scores. Example: A student scoring 4 in Programming (25%) contributes 1.0 to the total.*

---

## Materials & Resources

**Required (Free):**
- API Combat account (free tier) — [apicombat.com](https://apicombat.com)
- Text editor (VS Code recommended)
- Terminal access (Command Prompt, PowerShell, or Bash)
- Python 3.x with `requests` library (or equivalent in student's preferred language)

**Provided with Education Mode (Free for Accredited Institutions):**
- Private class instance — students battle each other, not the public player base
- Student progress tracking dashboard
- Custom challenge assignments tied to specific endpoints
- Class-specific leaderboards and tournament infrastructure
- Guild Wars team battle functionality

**Optional:**
- GitHub accounts for version control activities (Weeks 4–5)
- Postman for visual API exploration (Week 1)
- Jupyter Notebooks for data analysis and visualization (Weeks 3–4)

---

## Differentiation

**For advanced students:**
- Implement webhook notifications for real-time battle results
- Build a web dashboard that displays live battle updates via WebSockets
- Analyze opponent strategies and build adaptive counter-strategies
- Extend the bot with command-line arguments for configurable behavior

**For students needing additional support:**
- Provide starter code templates with comments indicating where to add logic
- Pair programming during automation activities (Weeks 2–3)
- Reduce battle count requirements while maintaining all concept areas
- Offer structured debugging checklists for common API errors

---

## What This Lesson Plan Doesn't Include

This document gives you everything you need for a compelling five-week unit. But the full Education Mode experience includes tools that a lesson plan can't capture:

- **Live progress dashboards** — See every student's API calls, battles played, strategies created, and win rates in real time
- **Custom challenge builder** — Create assignments tied to specific endpoints with automated success criteria
- **Automated milestone notifications** — Students hit checkpoints, you get notified without checking manually
- **Tournament infrastructure** — Bracket generation, live projected leaderboards, and match scheduling for class-wide events
- **Curriculum extensions** — WebSocket modules, OAuth integration labs, advanced rate-limiting exercises, and more
- **Semester-long pacing guides** — Expand this 5-week unit into a full 15-week course with intermediate and advanced modules

These are the tools that turn a good lesson plan into a curriculum ecosystem.

---

## Get Started This Semester

This lesson plan is yours to use — no strings attached. If you want the platform and tools behind it:

1. **Request Education Mode access** at [learnedgeek.com/Contact](https://learnedgeek.com/Contact)
2. Include your institution name, course title, class size, and semester dates
3. I'll set up your private instance and walk you through the educator dashboard

**Education Mode is free for accredited institutions.** K–12, universities, bootcamps — if you're teaching, you're in.

I built API Combat because students deserve better than todo-list APIs. If this lesson plan resonates, [let's talk about what a full semester looks like](https://learnedgeek.com/Contact).

---

*This lesson plan accompanies [Teaching REST APIs Through Gaming: Why API Combat Works in the Classroom](/Blog/Post/teaching-rest-apis-through-gaming). For the student-facing tutorial, see [Your First Battle: A Complete Walkthrough](/Blog/Post/your-first-api-combat-battle).*

---

*Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>*
