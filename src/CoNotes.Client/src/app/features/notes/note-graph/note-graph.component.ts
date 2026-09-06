import { AfterViewInit, Component, ElementRef, OnDestroy, inject, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import cytoscape, { Core } from 'cytoscape';

import { NoteService } from '../note.service';

@Component({
  selector: 'app-note-graph',
  template: `
    <h1>筆記關係圖</h1>
    <div #graphHost style="width: 100%; height: 600px; border: 1px solid black;"></div>
  `,
  styles: [],
})
export class NoteGraphComponent implements AfterViewInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly noteService = inject(NoteService);

  private readonly graphHost = viewChild.required<ElementRef<HTMLDivElement>>('graphHost');
  private cy: Core | null = null;

  ngAfterViewInit(): void {
    this.noteService.getGraph().subscribe((graph) => {
      this.cy = cytoscape({
        container: this.graphHost().nativeElement,
        elements: [
          ...graph.nodes.map((node) => ({ data: { id: node.noteId, label: node.title } })),
          ...graph.edges.map((edge) => ({
            data: { source: edge.sourceNoteId, target: edge.targetNoteId },
          })),
        ],
        style: [
          { selector: 'node', style: { label: 'data(label)' } },
          { selector: 'edge', style: { 'target-arrow-shape': 'triangle', 'curve-style': 'bezier' } },
        ],
        layout: { name: 'cose' },
      });

      this.cy.on('tap', 'node', (event) => {
        const noteId = event.target.id();
        this.router.navigate(['/notes', noteId, 'edit']);
      });
    });
  }

  ngOnDestroy(): void {
    this.cy?.destroy();
  }
}
