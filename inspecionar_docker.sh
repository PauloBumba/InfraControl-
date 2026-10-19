```bash
#!/bin/bash

# Listar todos os containers da máquina
docker ps -q --format "{{.ID}} {{.Status}}"
```

Este script Bash utiliza o comando `docker ps` para listar todos os containers da máquina local. O argumento `-q` é usado para exibir apenas o ID do container e o argumento `--format "{{.ID}} {{.Status}}"` é usado para formatear a saída para exibir apenas o ID e o Status de cada container.
