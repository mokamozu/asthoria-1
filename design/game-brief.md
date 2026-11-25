# Game Brief: Athoria

<!-- Escrito a partir do que o criador pediu nas conversas de 23–24/11/2026 e dos
documentos do projeto (claude/Athoria_Onboarding_Unificado.md e
claude/Athoria_Decisoes_de_Canon.md no Projeto do claude.ai). O jogo já existe
(brownfield): o "Features" marca o que já está construído. -->

## In your words
"criar um jogo no estilo rpg doom like no unity"

Pedidos do criador ao longo do desenvolvimento (palavras dele):
- "aq está uma coletania de assets variados mas todos na tematica que eu quero para os graficos do projeto, todos são da vibe de ps1 low poly"
- "o menu de seleção pode ser mais como essa imagens, quando vc passa o mouse em cima tem uma animação da personagem mudando"
- "essa é a ui base para os menus do jogo, a segunda imagem é uma referencia para recriar o menu principal de forma minimalista, queria um grende vitral com a lua e as nuvens de fundo"
- "Vamos adicionar as outras cavaleiras ao jogo, usando os novos assets, de braços e as armas, tmb vamos colocar uma seta no menu de seleção de cavaleiras para fazer um menu rotatório"
- "as cavaleiras n devem trocar de armas, no inventário deve ter apenas 4 altos de anéis e de amuleto, as armaduras vão ser somente estética"

**Pitch:** Um RPG de fantasia sombria em primeira pessoa, no visual low poly de PS1, em que você joga com as Cavaleiras lendárias de Athoria explorando as Terras Alagadas e suas masmorras.

**Genre:** RPG de ação em primeira pessoa ("doom like") com exploração, loot e masmorras

**Controls:** WASD para andar, mouse para mirar, clique para atacar (segurar = pesado), botão direito para defender, 1–2 armas da cavaleira, 3–4 itens rápidos, Q habilidade, E interagir, I/Tab inventário, F5/F9 salvar/carregar, Esc pausa *(inferred (strong))*

**Question:** Explorar e lutar em primeira pessoa com cada cavaleira, com seu kit fixo, é divertido? *(guessed)*

**Hooks:** Oito Cavaleiras jogáveis, cada uma com arma, braços e habilidade próprios; visual PS1 de fantasia sombria; o cânone de Athoria (Ipês, a peste do pântano, os Kazehara). *(inferred (strong))*

**Intent:** —

## Look and references
- Look: low poly de PS1 — resolução interna baixa e pixelada, texturas nítidas sem filtro, céu noturno PSX, névoa verde do pântano, tochas; menus em pixel art com vitral (lua e nuvens), pergaminho e pétalas de ipê.
- References: Hollow Knight (menu principal minimalista), o Asthoria antigo em GameMaker (artes, retratos e ideias), Diablo II / Path of Exile / Dark Souls (loot e combate) *(inferred (strong))*
- View: 3D · Camera: primeira pessoa, braços e arma visíveis

## First minute
Escolher a cavaleira na roleta do pergaminho, cair na estrada das Terras Alagadas à noite, seguir a placa até a Vila Junco, sacar a arma e enfrentar o primeiro lobo. *(guessed)*

## Challenge
Lobos e esqueletos pelo pântano desde os primeiros minutos; o Cavaleiro Corrompido na Capela Afogada guarda a chave da Cripta; o Guardião no fundo da Cripta. Morrer custa o progresso desde o último save. *(inferred (strong))*

## Core loop
- Explorar a região e descobrir locais
- Lutar (leve, pesado, defesa, esquiva, habilidade Q)
- Pegar loot (anéis, amuletos, poções, materiais, ouro) e subir de nível
- Abrir caminho para masmorras e chefes

## Goal & fail state
- Limpar as Terras Alagadas e a Cripta da Capela derrotando o Guardião *(guessed)*
- Morte volta ao último save (rápido ou automático)

## Features
- [x] Combate em primeira pessoa (leve, pesado, defesa, esquiva, projéteis)
- [x] 8 cavaleiras jogáveis com armas fixas, braços próprios e habilidade Q
- [x] Inimigos com IA (lobo, esqueleto, arqueiro, Cavaleiro Corrompido)
- [x] Loot com raridades e afixos; equipamento só de anéis (4) e amuleto
- [x] Região aberta Terras Alagadas (16 pedaços) e a Cripta da Capela
- [x] Salvar/carregar, menu principal, seleção em roleta, pausa
- [x] Visual PS1 com os pacotes low poly

## Your systems
- combate · in the slice
- cavaleiras jogáveis com kit fixo · in the slice
- inimigos e IA · in the slice
- loot (anéis e amuleto) · in the slice
- mundo aberto e masmorra · in the slice
- salvar e carregar · in the slice
- armaduras estéticas · later
- música (loops Mind's Eye) · later
- atributos (Força, Destreza...) · later

## Keep
- O visual PS1 low poly dos pacotes do criador *(you said)*
- Menu principal com o grande vitral, lua e nuvens, botões de vidro *(you said)*
- Seleção em pergaminho com faixas que trocam de pose ao passar o mouse, agora em roleta com setas *(you said)*
- Cada cavaleira com as próprias armas (não troca de arma) *(you said)*

## Avoid
- Trocar a arma das cavaleiras *(you said)*
- Armaduras com atributos (são só estéticas) *(you said)*
- Mais que amuleto + 4 anéis no equipamento *(you said)*
- Seraphine Gale (descartada) e os 5 atos de campanha do código antigo *(you said)*
- Mudar histórias, famílias, títulos ou características do cânone sem autorização

## Out of scope
- Multijogador *(guessed)*
- Outras regiões (Lacúnia, Costa dos Ventos, Kaminari no Kumi) por enquanto *(guessed)*
- Atributos de personagem (adiados pelo criador)

## Build order
1. Combate e uma cavaleira jogável na região (feito)
2. Inimigos, loot, mundo e save (feito)
3. Menus e visual PS1 (feito)
4. Oito cavaleiras com kits fixos (feito — testar o arpão e a escopeta da Anne)
5. Música e polimento de som
6. Próximas regiões e masmorras

---
**Kill condition:** —

**Who it's for / what they feel:** Fãs de RPG de fantasia sombria e de jogos retrô; sentir a força e o peso de cada Cavaleira. *(guessed)*

**Games you love / don't:** Hollow Knight (menu), Diablo II, Path of Exile, Dark Souls *(inferred (strong))*
