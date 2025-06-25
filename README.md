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
git clone https://github.com/stevmzz/TEC-MFS.git
cd TEC-MFS
dotnet restore
dotnet build
```

## Configuración

### Startup Projects (Visual Studio)
```bash
TecMFS.Controller → Start
TecMFS.GUI        → Start  
TecMFS.DiskNode   → None
TecMFS.Common     → None
```

### Ejecutar DiskNodes Manualmente
Para implementación RAID 5 completa, ejecutar 4 instancias de DiskNode desde terminales separadas:

**Terminal 1 (Nodo 1):**
```bash
cd "C:\ruta\a\tu\proyecto"
cd TecMFS.DiskNode
dotnet run --urls="http://localhost:5001"
```

**Terminal 2 (Nodo 2):**
```bash
cd "C:\ruta\a\tu\proyecto"
cd TecMFS.DiskNode
dotnet run --urls="http://localhost:5002"
```

**Terminal 3 (Nodo 3):**
```bash
cd "C:\ruta\a\tu\proyecto"
cd TecMFS.DiskNode
dotnet run --urls="http://localhost:5003"
```

**Terminal 4 (Nodo 4):**
```bash
cd "C:\ruta\a\tu\proyecto"
cd TecMFS.DiskNode
dotnet run --urls="http://localhost:5004"
```

## Uso

1. **Iniciar Controller y GUI** desde Visual Studio
2. **Ejecutar los 4 DiskNodes** desde terminales
3. **Usar la interfaz gráfica** para gestionar archivos PDF
4. **Monitorear estado** del sistema RAID desde la GUI

El sistema automáticamente distribuye los datos entre los nodos disponibles y proporciona tolerancia a fallos mediante paridad RAID 5.
