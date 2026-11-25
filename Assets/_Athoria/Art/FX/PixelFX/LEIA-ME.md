# Efeitos PixelFX — Athoria

Sprite sheets geradas com o PixelFX (https://github.com/patch-gg/pixelfx), licença MIT.
Mantenha LICENSE-pixelfx.txt e NOTICE-pixelfx.md junto com estes arquivos (créditos).

| Pasta | Efeito original | Ajustes | Uso no jogo |
|---|---|---|---|
| fx_morte_fumaca | spawn/poof | paleta de fumaça escura, faíscas bronze | morte de inimigos/bonecos (DeathFx) |
| fx_raio_kaminari | lightning/lightning-strike | — | Corte Relâmpago (Akane): raio em cada alvo atingido |
| fx_cura_evelyn | holy/heal-aoe | — | Kit de Campo (Evelyn) |
| fx_cura_ipe_rosa | holy/heal-aoe | paleta rosa (Ipê Rosa) | Bênção do Ipê Rosa (Layla) |
| fx_petalas_ipe | weather/cherry-blossoms | — | pétalas do ipê caindo na seleção de personagens |
| fx_bau_brilho | pickups/collect-burst | paleta dourada (do pickups/chest-open) | brilho ao abrir baús — o baú em si é 3D |

Para gerar de novo ou criar outros: `node tools/render.mjs <categoria/efeito> --sheet --opts '{"palette":[...]}'`
num clone do PixelFX, copie PNG + JSON para uma pasta aqui e rode Athoria > Efeitos > Importar sprite sheets PixelFX.
