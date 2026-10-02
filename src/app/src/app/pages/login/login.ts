import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly password = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected async submit() {
    this.error.set('');
    this.busy.set(true);
    try {
      await this.auth.login(this.password().trim());
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/recipes';
      void this.router.navigateByUrl(returnUrl);
    } catch {
      this.error.set(
        this.auth.passwordUnavailable()
          ? 'Serveren mangler husholdnings-kodeord (Household:Password).'
          : 'Forkert kodeord.',
      );
    } finally {
      this.busy.set(false);
    }
  }
}
