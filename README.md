# TEC Media File System (TEC-MFS)

Sistema de archivos distribuido que implementa RAID 5 bajo una arquitectura "Shared Disk Architecture" para almacenar y gestionar archivos PDF con tolerancia a fallos.

## Arquitectura del Sistema

- **Controller Node**: Servidor central que gestiona la distribución de datos y cálculo de paridad
- **Disk Nodes (4)**: Nodos de almacenamiento que guardan bloques de datos o paridad
- **GUI Application**: Interfaz gráfica para interacción del usuario
- **Protocolo HTTP/JSON**: Comunicación entre componentes

## Tecnologías

- **Lenguaje**: C# (.NET 8)
- **Framework Web**: ASP.NET Core Web API
- **GUI**: Windows Forms
- **Comunicación**: HTTP REST API con JSON
- **IDE**: Visual Studio 2022

## Estructura del Proyecto

```bash
TecMediaFileSystem.sln
├── TecMFS.Common/          # Modelos y DTOs compartidos
├── TecMFS.Controller/      # Servidor controlador central (Puerto 5000)
├── TecMFS.DiskNode/        # Nodos de almacenamiento (Puerto 5001+)
└── TecMFS.GUI/             # Interfaz gráfica de usuario
```

## Instalación

### Prerrequisitos
- .NET 8 SDK
- Visual Studio 2022
- Git

### Clonar e Instalar
```bash
git clone [URL_DEL_REPOSITORIO]
cd TecMediaFileSystem
dotnet restore
dotnet build
```

## Configuración

- Configurar startup projects según requiera. Recomendación:
```bash
TecMFS.Controller → Start
TecMFS.DiskNode   → Start
TecMFS.GUI        → Start
TecMFS.Common     → None
```