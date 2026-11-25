# Claude Code Game Studios -- Game Studio Agent Architecture

Indie game development managed through 49 coordinated Claude Code subagents.
Each agent owns a specific domain, enforcing separation of concerns and quality.

## Technology Stack

- **Engine**: Unity 6000.6.4f1 (Unity 6.6), Universal Render Pipeline
- **Language**: C# (assemblies Athoria.Core, Athoria.Gameplay, Athoria.UI, Athoria.Editor em `Assets/_Athoria/Scripts`)
- **Version Control**: Git (commits feitos pelo criador no GitHub Desktop)
- **Build System**: Unity Editor (menu Athoria → Reconstruir tudo gera prefabs, dados e cenas)
- **Asset Pipeline**: pacotes PS1 em `Assets/_Athoria/ThirdParty` (regras em `ThirdPartyImportSettings.cs`); arte do Asthoria antigo em `Assets/_Athoria/Art/Legacy`

> **Note**: Engine-specialist agents exist for Godot, Unity, and Unreal with
> dedicated sub-specialists. Use the set matching your engine.

## Athoria — regras deste projeto

- Cânone: `design/lore/Athoria_Decisoes_de_Canon.md` (cópia local; original no Projeto "Asthoria" do claude.ai) vale sobre a Bíblia (`Athoria_Onboarding_Unificado`),
  ambos no Projeto "Asthoria" do claude.ai. Não alterar histórias, famílias, títulos ou características sem autorização;
  propostas de jogo ficam marcadas como não confirmadas.
- O brief do jogo está em `design/game-brief.md`.
- Tudo que é gerado (prefabs, cenas, dados) vem dos construtores em `Assets/_Athoria/Scripts/Editor`; mudanças à mão
  nesses arquivos se perdem ao reconstruir. Depois de mudar código: focar o Unity, esperar compilar e rodar
  **Athoria → Reconstruir tudo**.
- Uma classe MonoBehaviour/ScriptableObject precisa estar num arquivo com o mesmo nome.
- O criador testa no Unity; peça antes de controlar o computador dele. Erros de compilação ficam em `Logs/Editor.log`.
- Textos do jogo e da interface em português do Brasil.

## Project Structure

@.claude/docs/directory-structure.md

## Engine Version Reference

<!-- ENGINE-REFERENCE-IMPORT: the line under this comment is engine-specific,
     and there is none until an engine is set. /setup-engine, the grow's record
     step and /project-settings (engine.name) write
     @docs/engine-reference/<engine>/VERSION.md here for the chosen engine. Skills
     that need the pinned version read that file on demand either way. -->


## Technical Preferences

`project.yaml` at the repo root is the primary config store — engine, specialists,
naming, platform, performance, modes. Skills resolve it via `resolve_config`
(see `.claude/docs/config-resolution.md`).

`.claude/docs/technical-preferences.md` is the **legacy fallback**, read on demand
when a key is absent from `project.yaml`. It is not imported here: before
`/setup-engine` runs it is almost entirely `[TO BE CONFIGURED]` placeholders, and
after it runs `project.yaml` holds the real values.

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
  (exempt: `active.md` appends, agent memory, a subagent's new file at the path
  its orchestrating skill named; see `.claude/rules/agent-memory.md`)
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction
- A skipped or dismissed question is not an answer: act on nothing, re-ask in
  plain text and wait
- Started outside this file's folder? Warn first: hooks, deny rules and the
  ask-first mode load only when Claude Code starts here

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

> **First session?** Say "make me a game" (`/prototype`), or run `/start` to begin
> the guided onboarding flow.

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

Read `.claude/docs/context-management.md` on demand — it is a reference, not
session context. Two of its conventions are load-bearing and cited by name
elsewhere in the repo, so they are restated here rather than lost:

- **`production/session-state/active.md` is the session checkpoint.** The file is
  the memory, not the conversation. Read it first after any compaction, crash, or
  `/clear`.
- **Helpers in `.claude/scripts/` emit observations, never verdicts.** A script
  that scores or judges will eventually contradict a mode or override it cannot
  see. (Cited by `artifact-check.sh` and `adr-dep-graph.sh`.)
