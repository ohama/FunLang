// FunLang Tutorial Navigation Component
class NavSidebar extends HTMLElement {
    connectedCallback() {
        this.innerHTML = `
        <nav class="sidebar">
            <div class="sidebar-header">
                <h1><a href="index.html">FunLang</a></h1>
                <span class="version">v0.6.0 Tutorial</span>
            </div>

            <div class="nav-section">
                <div class="nav-section-title">Getting Started</div>
                <ul class="nav-list">
                    <li><a href="index.html">Introduction</a></li>
                    <li><a href="building.html">Building</a></li>
                    <li><a href="cli.html">CLI Reference</a></li>
                </ul>
            </div>

            <div class="nav-section">
                <div class="nav-section-title">Usage</div>
                <ul class="nav-list">
                    <li><a href="interpreter.html">Interpreter</a></li>
                    <li><a href="wasm.html">WASM Compilation</a></li>
                </ul>
            </div>

            <div class="nav-section">
                <div class="nav-section-title">Language</div>
                <ul class="nav-list">
                    <li><a href="basics.html">Basics</a></li>
                    <li><a href="data-structures.html">Data Structures</a></li>
                    <li><a href="functions.html">Functions</a></li>
                    <li><a href="control-flow.html">Control Flow</a></li>
                    <li><a href="types.html">Types</a></li>
                    <li><a href="modules.html">Modules</a></li>
                    <li><a href="examples.html">Examples</a></li>
                </ul>
            </div>

            <div class="nav-section">
                <div class="nav-section-title">Reference</div>
                <ul class="nav-list">
                    <li><a href="errors.html">Errors & Warnings</a></li>
                </ul>
            </div>

            <div class="nav-section">
                <div class="nav-section-title">Implementation</div>
                <ul class="nav-list">
                    <li><a href="algorithms.html">Algorithms</a></li>
                    <li><a href="grammar.html">Grammar</a></li>
                </ul>
            </div>
        </nav>
        `;
    }
}

customElements.define('nav-sidebar', NavSidebar);
