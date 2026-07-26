import { Component, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';

interface ApiInfo {
  hostName: string;
  environment: string;
  serverTimeUtc: string;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  info = signal<ApiInfo | null>(null);
  error = signal<string | null>(null);

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.loadInfo();
  }

  loadInfo(): void {
    this.error.set(null);
    this.http.get<ApiInfo>('/api/info').subscribe({
      next: (data) => this.info.set(data),
      error: () => this.error.set('Could not reach the backend API.')
    });
  }
}
